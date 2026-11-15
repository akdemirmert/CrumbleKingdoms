using System;
using UnityEngine;
using UnityEngine.UI;

// Pause overlay: Resume, Restart, Settings (volumes and screen shake) and Main Menu.
// BattleHUD pauses/unpauses time; this class is only the menu.
public class HudPauseMenu : MonoBehaviour {
    UIFade fade;
    GameObject buttons, settings;

    public bool IsOpen => fade.Visible;

    public static HudPauseMenu Create(Transform parent, Action onResume) {
        RectTransform root = UIKit.Rect(parent, "Pause").Fill();
        HudPauseMenu m = root.gameObject.AddComponent<HudPauseMenu>();
        m.fade = root.gameObject.AddComponent<UIFade>();
        m.fade.hiddenOffset = Vector2.zero;
        m.fade.speed = 9f;
        Image dim = UIKit.Img(root, null, new Color(0.01f, 0.02f, 0.05f, 0.62f), "Dim");
        dim.rectTransform.Fill();
        dim.raycastTarget = true;   // blocks clicks to the battlefield

        RectTransform panel = UIKit.Panel(root, UIKit.PanelColor, 28f).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 600f));
        UIKit.Title(panel, "PAUSED", 66f, UIKit.TextColor).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(800f, 84f));

        RectTransform column = UIKit.Column(panel, 16f);
        column.Place(new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(460f, 440f));
        UIKit.Button(column, "Resume", ButtonStyle.Primary, onResume, new Vector2(440f, 90f), 40f);
        UIKit.Button(column, "Restart", ButtonStyle.Secondary, GameSession.Restart, new Vector2(440f, 76f), 32f);
        UIKit.Button(column, "Settings", ButtonStyle.Secondary, () => m.ShowSettings(true), new Vector2(440f, 76f), 32f);
        UIButton menu = UIKit.Button(column, "Main Menu", ButtonStyle.Secondary, ToMenu, new Vector2(440f, 76f), 32f);
        menu.clickSound = SoundId.UIBack;
        m.buttons = column.gameObject;

        RectTransform options = UIKit.Column(panel, 8f);
        options.Place(new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(800f, 440f));
        SettingsPanel.Build(options, false);
        RectTransform spacer = UIKit.Rect(options, "Spacer");
        spacer.Size(10f, 24f);
        UIButton back = UIKit.Button(options, "Back", ButtonStyle.Secondary, () => m.ShowSettings(false), new Vector2(260f, 76f), 32f);
        back.clickSound = SoundId.UIBack;
        m.settings = options.gameObject;

        m.ShowSettings(false);
        m.fade.Show(false, true);
        return m;
    }

    public void Open() {
        ShowSettings(false);
        fade.Show(true);
    }

    public void Close() { fade.Show(false); }

    // Esc inside the menu: leave the settings first. Returns true if the key was used here.
    public bool BackFromSettings() {
        if (!settings.activeSelf) return false;
        SFX.Play(SoundId.UIBack);
        ShowSettings(false);
        return true;
    }

    void ShowSettings(bool on) {
        buttons.SetActive(!on);
        settings.SetActive(on);
    }

    static void ToMenu() {
        BattleManager bm = BattleManager.I;
        GameSession.ToMenu(bm != null && bm.IsCampaign ? "levels" : "duel");
    }
}
