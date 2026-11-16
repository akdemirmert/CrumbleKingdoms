using TMPro;
using UnityEngine;
using UnityEngine.UI;

// End of battle: Victory / Defeat / "Player N wins!", animated stars (campaign wins), a stats table
// (shots, damage dealt, blocks lost for every fortress) and Next Level / Retry / Menu buttons.
public class HudResultScreen : MonoBehaviour {
    const float StarDelay = 0.55f, StarInterval = 0.38f;

    UIFade fade;
    RectTransform panel;
    Image[] stars;
    int earned, popped;
    float timer;

    public static HudResultScreen Create(Transform parent) {
        RectTransform root = UIKit.Rect(parent, "Result").Fill();
        HudResultScreen r = root.gameObject.AddComponent<HudResultScreen>();
        r.fade = root.gameObject.AddComponent<UIFade>();
        r.fade.hiddenOffset = new Vector2(0f, -60f);
        r.fade.hiddenScale = 0.9f;
        r.fade.speed = 4f;
        Image dim = UIKit.Img(root, null, new Color(0.01f, 0.02f, 0.05f, 0.55f), "Dim");
        dim.rectTransform.Fill();
        dim.raycastTarget = true;
        r.fade.Show(false, true);
        return r;
    }

    public void Show(BattleManager bm) {
        if (panel != null) Destroy(panel.gameObject);
        bool campaign = bm.IsCampaign;
        bool won = bm.PlayerWon;
        bool showStars = campaign && won;
        float height = showStars ? 760f : 620f;
        panel = UIKit.Panel(transform, UIKit.PanelColor, 30f).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, height));

        Headline(bm, out string title, out Color color, out string line);
        TextMeshProUGUI t = UIKit.Title(panel, title, 100f, color);
        t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(940f, 120f));
        t.characterSpacing = 4f;
        if (won || bm.IsHotSeat) UIKit.Gradient(t, UIKit.Lighten(color, 0.45f), color);
        UIKit.Label(panel, line, 30f, UIKit.TextDim, TextAlignmentOptions.Center, false).rectTransform
             .Place(new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(940f, 40f));

        float y = -196f;
        stars = null;
        earned = popped = 0;
        timer = 0f;
        if (showStars) {
            BuildStars(y, bm.Stars);
            y -= 150f;
        }
        BuildStats(bm, y);
        BuildButtons(bm, campaign, won);
        fade.ShowWarp(true, true);
    }

    static void Headline(BattleManager bm, out string title, out Color color, out string line) {
        if (bm.Winner == null) { title = "DRAW"; color = UIKit.TextColor; }
        else if (bm.IsHotSeat) {
            Fortress w = bm.Winner.fortresses.Count > 0 ? bm.Winner.fortresses[0] : null;
            title = (w != null ? w.DisplayName.ToUpperInvariant() : "TEAM " + (bm.Winner.id + 1)) + " WINS!";
            color = w != null ? UIKit.Lighten(w.TeamColor, 0.2f) : UIKit.Gold;
        } else if (bm.PlayerWon) { title = "VICTORY!"; color = UIKit.Gold; }
        else { title = "DEFEAT"; color = UIKit.Danger; }

        if (!bm.IsCampaign) line = bm.Setup.subtitle;
        else if (!bm.PlayerWon) line = bm.Setup.title + "  ·  the bots learn from every miss, so can you!";
        else if (GameSession.HasNextLevel) line = bm.Setup.title + "  ·  level " + (GameSession.LevelIndex + 2) + " unlocked!";
        else line = "The campaign is complete. Asgard bows to you!";
    }

    void BuildStars(float y, int count) {
        RectTransform row = UIKit.Row(panel, 22f);
        row.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600f, 140f));
        stars = new Image[3];
        for (int i = 0; i < 3; i++) {
            float size = i == 1 ? 132f : 108f;
            RectTransform slot = UIKit.Rect(row, "StarSlot");
            slot.Size(size, size);
            UIKit.Img(slot, UIKit.Icon(UIIcon.Star), new Color(1f, 1f, 1f, 0.12f), "Empty").rectTransform.Fill();
            stars[i] = UIKit.Img(slot, UIKit.Icon(UIIcon.Star), UIKit.Gold, "Star");
            stars[i].rectTransform.Fill();
            stars[i].rectTransform.localScale = Vector3.zero;
        }
        earned = Mathf.Clamp(count, 0, 3);
    }

    void BuildStats(BattleManager bm, float y) {
        RectTransform table = UIKit.Column(panel, 4f, TextAnchor.UpperCenter);
        table.Place(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, 260f));
        StatRow(table, "", "Shots", "Damage", "Blocks lost", UIKit.TextDim, 20f, null);
        foreach (Fortress f in bm.fortresses) {
            if (f == null) continue;
            StatRow(table, f.DisplayName, f.shotsFired.ToString(), Mathf.RoundToInt(f.damageDealt).ToString(), f.blocksLost.ToString(),
                    f.defeated ? UIKit.TextDim : UIKit.TextColor, 26f, f.TeamColor);
        }
        float seconds = bm.Elapsed;
        string time = Mathf.FloorToInt(seconds / 60f) + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");
        UIKit.Label(table, "Turns " + bm.Turn + "   ·   Time " + time, 22f, UIKit.TextDim).Size(900f, 40f);
    }

    static void StatRow(Transform table, string name, string shots, string damage, string lost, Color color, float size, Color? team) {
        RectTransform row = UIKit.Row(table, 0f, TextAnchor.MiddleLeft);
        row.Size(900f, size + 18f);
        RectTransform nameCell = UIKit.Rect(row, "Name");
        nameCell.Size(420f, size + 18f);
        if (team.HasValue)
            UIKit.Img(nameCell, UIKit.Circle, team.Value, "Team").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(18f, 18f));
        TextMeshProUGUI n = UIKit.Label(nameCell, name, size, color, TextAlignmentOptions.Left);
        n.overflowMode = TextOverflowModes.Ellipsis;
        n.rectTransform.Fill(48f, 0f, 0f, 0f);
        UIKit.Label(row, shots, size, color).Size(140f, size + 18f);
        UIKit.Label(row, damage, size, color).Size(160f, size + 18f);
        UIKit.Label(row, lost, size, color).Size(180f, size + 18f);
    }

    void BuildButtons(BattleManager bm, bool campaign, bool won) {
        RectTransform row = UIKit.Row(panel, 22f);
        row.Place(new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(940f, 92f));
        bool next = campaign && won && GameSession.HasNextLevel;
        string target = campaign ? "levels" : "duel";
        UIButton menu = UIKit.Button(row, "Menu", next || !won || !campaign ? ButtonStyle.Secondary : ButtonStyle.Primary,
                                     () => GameSession.ToMenu(target), new Vector2(230f, 84f), 34f);
        menu.clickSound = SoundId.UIBack;
        bool retryPrimary = !campaign || !won;
        UIKit.Button(row, campaign ? "Retry" : "Rematch", retryPrimary ? ButtonStyle.Primary : ButtonStyle.Secondary,
                     GameSession.Restart, new Vector2(260f, 84f), 34f);
        if (next) UIKit.Button(row, "Next Level", ButtonStyle.Primary, GameSession.NextLevel, new Vector2(320f, 84f), 38f);
    }

    // Stars pop in one after another once the panel has arrived.
    void Update() {
        if (stars == null || popped >= 3) return;
        timer += Time.unscaledDeltaTime;
        for (int i = 0; i < 3; i++) {
            float local = timer - StarDelay - i * StarInterval;
            if (i >= earned || local < 0f) continue;
            if (i >= popped) {
                popped = i + 1;
                SFX.Play(SoundId.Star, null, 0.9f, 1f + i * 0.12f);
            }
            float s = UIKit.EaseOutBack(local / 0.35f) * (1f + 0.15f * Mathf.Exp(-local * 6f));
            stars[i].rectTransform.localScale = new Vector3(s, s, 1f);
        }
        if (popped >= earned && timer > StarDelay + earned * StarInterval + 0.5f) popped = 3;   // animation done
    }
}
