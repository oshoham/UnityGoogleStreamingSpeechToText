using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class AccumulateText : MonoBehaviour {
    private string _accumulatedText = "";
    private string _interimText = "";
    private Text _text;
    private RectTransform _canvasRect;

    public void AddInterimTranscription(string interimTranscription) {
        _interimText = _accumulatedText == "" ? interimTranscription : " " + interimTranscription;
        SetText();
    }

    public void AddFinalTranscription(string finalTranscription) {
        if (_accumulatedText != "") {
            _accumulatedText += " ";
        }
        _accumulatedText += finalTranscription;
        _interimText = "";
        SetText();
    }

    private void Awake() {
        _text = gameObject.GetComponent<Text>();
        _canvasRect = _text.canvas.GetComponent<RectTransform>();
    }

    private void SetText() {
        float textHeight = LayoutUtility.GetPreferredHeight(_text.rectTransform);
        float parentHeight = _canvasRect.rect.height;
        if (textHeight > parentHeight) {
            _accumulatedText = "";
            _text.text = _interimText.Trim();
        } else {
            _text.text = _accumulatedText + _interimText;
        }
    }
} 
