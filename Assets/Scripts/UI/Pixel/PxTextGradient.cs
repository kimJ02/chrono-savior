using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 글자 위아래 색 변화 — Figma HUD 숫자(남은 초·COMBO)는 위가 흰색, 아래가 연회색이다(2026-10-24 캡처 확대로 확인 —
    /// Figma 코드 출력엔 효과가 안 나온다). 테두리(<see cref="Px.WithOutline"/>)보다 먼저 붙여야 테두리 색은 안 바뀐다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class PxTextGradient : BaseMeshEffect
    {
        public Color Top = Color.white;
        public Color Bottom = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts)
            {
                min = Mathf.Min(min, v.position.y);
                max = Mathf.Max(max, v.position.y);
            }
            float h = Mathf.Max(0.0001f, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                Color c = Color.Lerp(Bottom, Top, (v.position.y - min) / h);
                c.a *= v.color.a / 255f;
                v.color = c;
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
