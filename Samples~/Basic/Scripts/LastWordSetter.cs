using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LastWordSetter : MonoBehaviour {
    private Text _text;

    private void Awake() {
        _text = gameObject.GetComponent<Text>();
    }

    public void SetText(string transcription) {
        string[] words = transcription.Split(' ');
        string lastWord = words[words.Length - 1];
        _text.text = lastWord;
    }
}
