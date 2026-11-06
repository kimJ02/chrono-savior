using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 전투 HUD 오른쪽 아래 미니맵 — Figma "지도 1"(166:30) 자리에 **실제 맵**을 그린다: 땅·발판 선, 플레이어·몹·포탈 점
    /// (2026-11-06 정리 순서 5-4, 예전엔 `CombatHud` 안에 있었다). HUD가 만들고 매 갱신에 플레이어 위치·포탈 점 자리를 넘긴다.
    /// 무대(일반·보스)가 바뀌면 발판 선을 다시 그리고, 몹 위치는 0.15초마다 전투 창구(`WorldView.Combat`)에서 읽는다.
    /// </summary>
    public sealed class CombatMinimapView
    {
        // 맵 가로(26유닛)를 128px에, 높이는 땅이 58·발판 1층마다 약 11px(Figma 그림의 층 간격 16px에 가깝게 4층이 들어가도록)
        const float GroundY = 58f, PxPerUnitY = 8f;
        const float EnemyScanInterval = 0.15f; // 몹 위치는 0.15초마다(매 프레임 읽을 필요 없다)
        static readonly Color LineColor = Px.Ink;
        static readonly Color EnemyColor = Px.Hex(0xea323c);
        static readonly Color PlayerColor = Color.white;
        static readonly Color PortalColor = Px.Hex(0x0cf1ff);

        readonly RectTransform root;
        readonly float width, height;
        readonly List<Image> platformLines = new List<Image>();
        readonly List<Image> enemyDots = new List<Image>();
        readonly Image playerDot, portalDot;
        readonly List<Vector2> enemyPositions = new List<Vector2>();
        bool? drawnBossArena;
        float enemyScanTimer;

        /// <summary>그린 땅·발판 선 수(테스트가 읽는다).</summary>
        public int PlatformLineCount => platformLines.Count;

        public CombatMinimapView(Transform parent, Rect box)
        {
            width = box.width;
            height = box.height;
            root = Px.Node(parent, "Minimap", box.x, box.y, box.width, box.height);
            portalDot = Px.Fill(root, "Portal", PortalColor, 0f, 0f, 2f, 3f);
            playerDot = Px.Fill(root, "Player", PlayerColor, 0f, 0f, 2f, 2f);
        }

        /// <param name="enemies">그릴 몹 위치 — null이면 전투 창구에서 읽는다(캡처·테스트는 가짜 값을 넘긴다).</param>
        /// <param name="portal">포탈 점 자리 — null이면 안 그린다.</param>
        public void Refresh(Vector2 player, IList<Vector2> enemies, Vector2? portal)
        {
            if (drawnBossArena != FieldLayout.IsBossArena) RebuildPlatforms();

            var p = MapPoint(player);
            Place(playerDot, p.x - 1f, p.y - 2f);

            portalDot.enabled = portal.HasValue;
            if (portal.HasValue) Place(portalDot, MapPoint(portal.Value).x - 1f, GroundY - 3f);

            if (enemies == null)
            {
                enemyScanTimer -= Time.unscaledDeltaTime;
                if (enemyScanTimer <= 0f)
                {
                    enemyScanTimer = EnemyScanInterval;
                    var view = WorldView.Combat;
                    if (view != null) view.CopyEnemyPositions(enemyPositions);
                    else enemyPositions.Clear(); // 전투 씬이 없으면 점도 없다
                }
                enemies = enemyPositions;
            }
            while (enemyDots.Count < enemies.Count) enemyDots.Add(Px.Fill(root, "Enemy", EnemyColor, 0f, 0f, 2f, 2f));
            for (int i = 0; i < enemyDots.Count; i++)
            {
                bool on = i < enemies.Count;
                enemyDots[i].enabled = on;
                if (on)
                {
                    var e = MapPoint(enemies[i]);
                    Place(enemyDots[i], e.x - 1f, e.y - 2f);
                }
            }
            playerDot.transform.SetAsLastSibling(); // 플레이어 점이 몹 점 위에
        }

        /// <summary>지난 몹 점을 지운다 — 전투 씬이 없을 때(타이틀·전환 중) 남기지 않게(Codex 2b 리뷰).</summary>
        public void ForgetEnemies() => enemyPositions.Clear();

        Vector2 MapPoint(Vector2 world)
        {
            float span = Mathf.Max(0.01f, FieldBounds.MaxX - FieldBounds.MinX);
            float x = (world.x - FieldBounds.MinX) / span * width;
            float y = GroundY - (world.y - FieldBounds.GroundY) * PxPerUnitY;
            return new Vector2(Mathf.Round(Mathf.Clamp(x, 0f, width - 1f)), Mathf.Round(Mathf.Clamp(y, 1f, height - 1f)));
        }

        void RebuildPlatforms()
        {
            drawnBossArena = FieldLayout.IsBossArena;
            foreach (var l in platformLines)
                if (l != null) { if (Application.isPlaying) Object.Destroy(l.gameObject); else Object.DestroyImmediate(l.gameObject); }
            platformLines.Clear();
            // 땅 — 맨 아래 한 줄
            platformLines.Add(Px.Fill(root, "Ground", LineColor, 0f, GroundY, width, 1f));
            var table = FieldLayout.Platforms;
            for (int i = 0; i < table.GetLength(0); i++)
            {
                float cx = table[i, 0], w = table[i, 2];
                var a = MapPoint(new Vector2(cx - w / 2f, FieldLayout.PlatformTopY(i)));
                var b = MapPoint(new Vector2(cx + w / 2f, FieldLayout.PlatformTopY(i)));
                platformLines.Add(Px.Fill(root, "Platform" + i, LineColor, a.x, a.y, Mathf.Max(1f, b.x - a.x), 1f));
            }
            foreach (var l in platformLines) l.transform.SetAsFirstSibling();
        }

        static void Place(Image img, float x, float y) =>
            ((RectTransform)img.transform).anchoredPosition = new Vector2(x, -y);
    }
}
