using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The four weapon slots of the human player whose turn it is: rendered icon, name, key number,
// cooldown overlay with the turns left, highlight on the selected weapon and a tooltip.
// Clicking a slot selects it through HumanShotController (same rules as the 1-4 keys).
public class HudWeaponBar : MonoBehaviour {
    const float SlotSize = 124f, Gap = 16f, Pad = 18f;

    class Slot {
        public UIButton button;
        public RectTransform visual, iconHolder;
        public Image frame, glow, shade;
        public TextMeshProUGUI nameText, cooldownText;
        public GameObject cooldown;
        public int shownCooldown = -1;
        public float lift, pulse;
    }

    readonly Slot[] slots = new Slot[4];
    UIFade fade;
    Fortress bound;
    int boundClass = -1;

    public static HudWeaponBar Create(Transform parent) {
        float width = SlotSize * 4f + Gap * 3f + Pad * 2f;
        RectTransform root = UIKit.Rect(parent, "WeaponBar").Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(width, SlotSize + 58f));
        HudWeaponBar bar = root.gameObject.AddComponent<HudWeaponBar>();
        bar.fade = root.gameObject.AddComponent<UIFade>();
        bar.fade.hiddenOffset = new Vector2(0f, -170f);
        UIKit.Panel(root, UIKit.PanelColor, 26f).Fill(0f, 0f, 0f, 22f);
        for (int i = 0; i < 4; i++) bar.slots[i] = bar.BuildSlot(root, i);
        bar.fade.Show(false, true);
        return bar;
    }

    public void SetVisible(bool visible) {
        if (fade.Visible != visible) fade.Show(visible);
    }

    // Shows the weapons of this fortress (icons are rebuilt only when the class changes).
    public void Bind(Fortress f) {
        bound = f;
        if (f == null || (int)f.HeroClass == boundClass) return;
        boundClass = (int)f.HeroClass;
        for (int i = 0; i < 4; i++) {
            Slot s = slots[i];
            WeaponDef def = f.Weapon(i);
            for (int k = s.iconHolder.childCount - 1; k >= 0; k--) Destroy(s.iconHolder.GetChild(k).gameObject);
            UIKit.WeaponIcon(s.iconHolder, def.id, SlotSize - 12f).Fill();
            s.nameText.text = def.name;
            s.glow.color = UIKit.Alpha(def.color, 0f);
            s.button.fxColor = UIKit.Lighten(def.color, 0.25f);   // the click pulse takes the weapon's colour
            s.shownCooldown = -1;
        }
    }

    Slot BuildSlot(RectTransform root, int index) {
        Slot s = new Slot();
        float x = Pad + index * (SlotSize + Gap);
        RectTransform holder = UIKit.Rect(root, "Slot " + (index + 1)).Place(new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(SlotSize, SlotSize + 34f));
        s.visual = UIKit.Rect(holder, "Visual").Fill();

        s.glow = UIKit.Img(s.visual, UIKit.Shadow, new Color(1f, 1f, 1f, 0f), "Glow");
        s.glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(SlotSize + 60f, SlotSize + 60f));
        s.frame = UIKit.RoundedImg(s.visual, UIKit.Secondary, 20f, "Frame");
        s.frame.rectTransform.Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(SlotSize, SlotSize));
        s.frame.raycastTarget = true;
        s.iconHolder = UIKit.Rect(s.frame.transform, "Icon").Fill(6f, 6f, 6f, 6f);

        s.cooldown = UIKit.Rect(s.frame.transform, "Cooldown").Fill(6f, 6f, 6f, 6f).gameObject;
        s.shade = UIKit.RoundedImg(s.cooldown.transform, new Color(0.03f, 0.04f, 0.08f, 0.78f), 16f, "Shade");
        s.shade.rectTransform.Fill();
        s.cooldownText = UIKit.Title(s.cooldown.transform, "", 54f, UIKit.TextColor);
        s.cooldownText.rectTransform.Fill(0f, 18f, 0f, 0f);
        UIKit.Label(s.cooldown.transform, "turns", 18f, UIKit.TextDim).rectTransform
             .Place(new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(100f, 24f));

        RectTransform key = UIKit.Img(s.frame.transform, UIKit.Circle, new Color(0.04f, 0.05f, 0.09f, 0.9f), "Key").rectTransform
                                 .Place(new Vector2(0f, 1f), new Vector2(-8f, 8f), new Vector2(36f, 36f));
        UIKit.Label(key, (index + 1).ToString(), 22f, UIKit.Gold).rectTransform.Fill(0f, 1f, 0f, 0f);

        s.nameText = UIKit.Label(s.visual, "", 22f, UIKit.TextColor);
        s.nameText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(SlotSize + 20f, 30f));

        s.button = holder.gameObject.AddComponent<UIButton>();
        s.button.clickSound = null;   // HumanShotController plays the selection click
        s.button.fx = ClickFxStyle.Weapon;
        s.button.onClick = () => { if (BattleManager.I != null) BattleManager.I.Human.SelectSlot(index); };
        TooltipTrigger.Add(holder, () => bound != null ? bound.Weapon(index).title : "", () => TooltipBody(index));
        return s;
    }

    string TooltipBody(int index) {
        if (bound == null) return "";
        string body = UIKit.WeaponInfo(bound.Weapon(index));
        int cd = bound.cooldowns[index];
        return body + (cd > 0 ? "\n<color=#FF7B7B>Ready in " + cd + (cd == 1 ? " turn" : " turns") + "</color>" : "\n<color=#4CD38A>Ready</color>");
    }

    void Update() {
        if (bound == null) return;
        for (int i = 0; i < 4; i++) {
            Slot s = slots[i];
            WeaponDef def = bound.Weapon(i);
            int cd = Mathf.Max(0, bound.cooldowns[i]);
            bool selected = bound.selectedSlot == i;
            if (cd != s.shownCooldown) {
                if (s.shownCooldown > 0 && cd == 0) {   // the weapon is ready again: pulse the slot
                    s.pulse = 1f;
                    ClickFX.Play(RectTransformUtility.WorldToScreenPoint(null, s.frame.rectTransform.position),
                                 ClickFxStyle.Weapon, UIKit.Lighten(def.color, 0.3f), 64f);
                }
                s.shownCooldown = cd;
                s.cooldown.SetActive(cd > 0);
                s.cooldownText.text = cd.ToString();
                s.button.Interactable = cd == 0;
            }
            float k = UIKit.Approach(12f);
            s.lift = Mathf.Lerp(s.lift, selected ? 1f : 0f, k);
            s.visual.anchoredPosition = new Vector2(0f, 12f * s.lift);
            s.frame.color = Color.Lerp(s.frame.color, selected ? def.color : UIKit.Secondary, k);
            s.pulse = Mathf.MoveTowards(s.pulse, 0f, Time.unscaledDeltaTime * 2.2f);
            s.glow.color = UIKit.Alpha(def.color, Mathf.Max(0.8f * s.lift, 0.95f * s.pulse));
            s.frame.rectTransform.localScale = Vector3.one * (1f + 0.16f * s.pulse * s.pulse);
            s.nameText.color = selected ? UIKit.Lighten(def.color, 0.4f) : UIKit.TextDim;
        }
    }
}
