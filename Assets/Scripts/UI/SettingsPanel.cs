using System;
using TMPro;
using UnityEngine;

// Volume sliders and option switches, shared by the main menu and the pause menu.
public static class SettingsPanel {
    const float LabelWidth = 300f, ControlWidth = 460f, RowHeight = 62f;

    // Builds the rows into `parent` (a vertical layout). full = also fullscreen and "reset progress".
    public static void Build(Transform parent, bool full, Action onProgressReset = null) {
        UISlider.Create(Row(parent, "Master volume"), GameSettings.MasterVolume, UIKit.Gold, ControlWidth, v => GameSettings.MasterVolume = v);
        UISlider.Create(Row(parent, "Music"), GameSettings.MusicVolume, UIKit.Gold, ControlWidth, v => GameSettings.MusicVolume = v);
        UISlider.Create(Row(parent, "Sound effects"), GameSettings.SfxVolume, UIKit.Gold, ControlWidth, v => GameSettings.SfxVolume = v);
        UISwitch.Create(Row(parent, "Screen shake"), GameSettings.ScreenShake, UIKit.Good, v => GameSettings.ScreenShake = v);
        if (!full) return;
        UISwitch.Create(Row(parent, "Fullscreen"), GameSettings.Fullscreen, UIKit.Good, v => GameSettings.Fullscreen = v);
        ResetRow(parent, onProgressReset);
    }

    // Label on the left, control area on the right (returned).
    static Transform Row(Transform parent, string label) {
        RectTransform row = UIKit.Row(parent, 20f, TextAnchor.MiddleLeft);
        row.Size(LabelWidth + ControlWidth + 20f, RowHeight);
        UIKit.Label(row, label, 30f, UIKit.TextColor, TextAlignmentOptions.Left).Size(LabelWidth, RowHeight);
        RectTransform slot = UIKit.Rect(row, "Control");
        slot.Size(ControlWidth, RowHeight);
        UIKit.Layout<UnityEngine.UI.HorizontalLayoutGroup>(slot, 0f, TextAnchor.MiddleLeft);
        return slot;
    }

    // "Reset campaign progress" asks for confirmation before erasing anything.
    static void ResetRow(Transform parent, Action onProgressReset) {
        Transform slot = Row(parent, "Campaign progress");
        RectTransform ask = UIKit.Rect(slot, "Ask");
        ask.Size(ControlWidth, RowHeight);
        RectTransform confirm = UIKit.Row(slot, 12f, TextAnchor.MiddleLeft);
        confirm.Size(ControlWidth, RowHeight);
        TextMeshProUGUI done = UIKit.Label(slot, "Progress erased.", 26f, UIKit.Good, TextAlignmentOptions.Left);
        done.Size(ControlWidth, RowHeight);

        UIButton reset = UIKit.Button(ask, "Reset progress", ButtonStyle.Danger, null, new Vector2(260f, 54f), 24f);
        ((RectTransform)reset.transform).Place(new Vector2(0f, 0.5f), Vector2.zero, new Vector2(260f, 54f));
        UIKit.Label(confirm, "Sure?", 26f, UIKit.Danger, TextAlignmentOptions.Left).Size(80f, RowHeight);
        UIButton no = UIKit.Button(confirm, "Cancel", ButtonStyle.Secondary, null, new Vector2(150f, 54f), 24f);
        UIButton yes = UIKit.Button(confirm, "Erase", ButtonStyle.Danger, null, new Vector2(150f, 54f), 24f);
        no.clickSound = SoundId.UIBack;

        void State(int s) {
            ask.gameObject.SetActive(s == 0);
            confirm.gameObject.SetActive(s == 1);
            done.gameObject.SetActive(s == 2);
        }
        reset.onClick = () => State(1);
        no.onClick = () => State(0);
        yes.onClick = () => {
            SaveData.ResetProgress();
            State(2);
            onProgressReset?.Invoke();
        };
        State(0);
    }
}
