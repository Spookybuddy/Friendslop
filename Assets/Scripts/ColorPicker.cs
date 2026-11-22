using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ColorPicker : MonoBehaviour
{
    private Color picked = Color.white;
    private string colorHex = "FFFFFF";
    [Tooltip("The area of the color picker.")]
    public RawImage image;
    private readonly Vector3[] corners = new Vector3[4];
    private Rect area;
    [Tooltip("The display for the player's current color.")]
    public RawImage colorDisplay;

    private void Start()
    {
        //Create area detection
        image.rectTransform.GetWorldCorners(corners);
        area = new Rect(corners[0], corners[2] - corners[0]);

        //Load saved color
        if (PlayerPrefs.HasKey("Color")) colorHex = PlayerPrefs.GetString("Color");
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out Color c)) picked = c;
        colorDisplay.color = picked;
    }

    //Button function
    public void ClickPicker()
    {
        picked = GetPixel();
        if (picked.Equals(Color.clear)) return;
        colorHex = ColorUtility.ToHtmlStringRGB(picked);
        PlayerPrefs.SetString("Color", colorHex);
        colorDisplay.color = picked;
    }

    //Returns the selected pixel color
    private Color GetPixel()
    {
        if (!area.Contains(Input.mousePosition)) return Color.clear;
        Texture2D texture = image.mainTexture as Texture2D;
        Vector3 point = Input.mousePosition;
        point -= corners[0];
        point.x /= area.width;
        point.y /= area.height;
        return texture.GetPixelBilinear(point.x, point.y);
    }
}