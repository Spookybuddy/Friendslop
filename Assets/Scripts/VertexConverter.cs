using UnityEngine;

public class VertexConverter : MonoBehaviour
{
    //This takes the mesh's vertex color's red channel and moves it to the alpha channel instead
    void Start()
    {
        Mesh m = GetComponent<MeshFilter>().sharedMesh;
        Color[] colors = new Color[m.vertexCount];
        for (int i = 0; i < m.vertexCount; i++) {
            if (m.colors[i].r > 0) {
                colors[i].a = 1 - m.colors[i].r;
                colors[i].r = 0;
                colors[i].b = m.colors[i].b;
                colors[i].g = m.colors[i].g;
            } else colors[i] = m.colors[i];
        }
        m.colors = colors;
    }
}