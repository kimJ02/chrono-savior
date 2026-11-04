using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma 화면 하나(2026-10-24) — 처음 한 번 그림·글자를 만들고(<see cref="Build"/>), 게임 상태를 보고 켜고 끄며
    /// (<see cref="WantsVisible"/>), 켜져 있는 동안 값을 갱신한다(<see cref="Refresh"/>).
    ///
    /// 화면은 두 겹이다: <see cref="Root"/> = 화면 전체(어두운 막처럼 판 밖까지 덮을 것), <see cref="Content"/> = 가운데 640×360 판
    /// (Figma 프레임 — 레이어 좌표를 그대로 넣는다).
    ///
    /// **키보드**: 버튼을 <see cref="AddNav"/>로 등록하면 방향키로 고르고 Enter·Space·Z로 누른다. 여러 화면이 겹쳐 있으면
    /// 맨 위 화면만 키를 받는다. <see cref="Modal"/> 화면(설정 창 등)은 떠 있는 동안 ESC를 먼저 가져간다(<see cref="UiModal"/> —
    /// 그래야 설정 창을 닫으려던 ESC가 일시정지까지 풀지 않는다).
    /// </summary>
    public abstract class PxScreen : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        /// <summary>판 뒤에 깔리는 화면 전체 층(어두운 막 등) — <see cref="Content"/>보다 먼저 그린다.</summary>
        public RectTransform Back { get; private set; }
        public RectTransform Content { get; private set; }
        public bool IsShown => Root != null && Root.gameObject.activeSelf;

        protected virtual PixelCanvas.Layer Layer => PixelCanvas.Layer.Window;
        /// <summary>떠 있는 동안 ESC를 이 화면이 쓴다(<see cref="OnEscape"/>).</summary>
        protected virtual bool Modal => false;

        protected abstract void Build();
        protected abstract bool WantsVisible { get; }
        protected virtual void Refresh() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        /// <summary>모달 화면에서 ESC — 보통 닫기.</summary>
        protected virtual void OnEscape() { }
        /// <summary>이 화면이 키를 받는 프레임마다(버튼 고르기와 따로 키를 쓰는 화면 — 설정 창의 줄 고르기 등).</summary>
        protected virtual void OnKeys() { }

        // ── 키보드 선택 ──
        protected enum NavLayout { Horizontal, Vertical, Grid }
        protected virtual NavLayout Layout => NavLayout.Horizontal;
        /// <summary><see cref="NavLayout.Grid"/>일 때 한 줄 칸 수.</summary>
        protected virtual int NavColumns => 1;
        /// <summary>화면이 켜질 때 고를 버튼.</summary>
        protected virtual int DefaultNav => 0;

        protected readonly List<PxButton> Nav = new List<PxButton>();
        public int NavIndex { get; private set; } = -1;

        static readonly List<PxScreen> all = new List<PxScreen>();
        static int nextOrder;
        int order;
        bool built, modalPushed;
        int shownFrame = -1;

        protected virtual void Awake()
        {
            order = nextOrder++;
            all.Add(this);
        }

        protected virtual void OnDestroy()
        {
            all.Remove(this);
            if (modalPushed) UiModal.Pop();
        }

        void Start() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (built) return;
            var canvas = PixelCanvas.Instance != null ? PixelCanvas.Instance : GetComponent<PixelCanvas>();
            if (canvas == null) return;
            built = true;
            Root = Px.Stretch(canvas.LayerRoot(Layer), GetType().Name);
            Back = Px.Stretch(Root, "Back");
            Content = Px.Board(Root);
            Build();
            Root.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            EnsureBuilt();
            if (!built) return;
            bool show = WantsVisible;
            if (show != IsShown) SetShown(show);
            if (!show) return;
            HandleKeys();
            Refresh();
        }

        void SetShown(bool show)
        {
            Root.gameObject.SetActive(show);
            if (show)
            {
                shownFrame = Time.frameCount;
                if (Modal && !modalPushed) { UiModal.Push(); modalPushed = true; }
                OnShown();
                if (Nav.Count > 0) SelectNav(DefaultNav);
            }
            else
            {
                if (modalPushed) { UiModal.Pop(); modalPushed = false; }
                OnHidden();
            }
        }

        /// <summary>
        /// 배치 모드 캡처용 — 게임 상태와 상관없이 켜고 한 번 갱신한다. <paramref name="select"/>가 false면 키보드 선택 표시를 끈다
        /// (Figma 그림과 1:1로 비교할 때 — Figma엔 고른 상태가 없다).
        /// </summary>
        public void ShowForCapture(bool select = true)
        {
            EnsureBuilt();
            Root.gameObject.SetActive(true);
            OnShown();
            if (Nav.Count > 0) SelectNav(DefaultNav);
            if (!select)
                foreach (var b in Nav) b.Selected = false;
            Refresh();
        }

        /// <summary>캡처용 — 끈다.</summary>
        public void HideForCapture()
        {
            EnsureBuilt();
            Root.gameObject.SetActive(false);
        }

        protected PxButton AddNav(PxButton b)
        {
            int i = Nav.Count;
            Nav.Add(b);
            b.OnHover = () => SelectNav(i);
            return b;
        }

        protected void SelectNav(int i)
        {
            if (Nav.Count == 0) return;
            NavIndex = Mathf.Clamp(i, 0, Nav.Count - 1);
            for (int k = 0; k < Nav.Count; k++) Nav[k].Selected = k == NavIndex;
            OnNavChanged(NavIndex);
        }

        /// <summary>고른 버튼이 바뀌었을 때(◆ 표시를 옮기는 화면 등).</summary>
        protected virtual void OnNavChanged(int index) { }

        /// <summary>이 화면이 지금 키를 받는가 — 켜진 화면 중 층이 가장 높고(같으면 나중에 만든) 화면만.</summary>
        protected bool HasKeyFocus
        {
            get
            {
                foreach (var s in all)
                {
                    if (s == this || !s.IsShown || !s.TakesKeys) continue;
                    if (s.Layer > Layer || (s.Layer == Layer && s.order > order)) return false;
                }
                return true;
            }
        }

        /// <summary>키를 받는 화면인가(버튼이 있거나 모달).</summary>
        protected virtual bool TakesKeys => Nav.Count > 0 || Modal;

        void HandleKeys()
        {
            if (Time.frameCount == shownFrame || !HasKeyFocus) return; // 이 화면을 연 키가 바로 눌리지 않게
            if (Modal && GameInput.PauseDown) { OnEscape(); return; }
            OnKeys();
            if (Nav.Count == 0) return;

            int cols = Layout == NavLayout.Horizontal ? Nav.Count : Layout == NavLayout.Vertical ? 1 : Mathf.Max(1, NavColumns);
            int dx = (GameInput.MenuRightDown ? 1 : 0) - (GameInput.MenuLeftDown ? 1 : 0);
            int dy = (GameInput.MenuDownDown ? 1 : 0) - (GameInput.MenuUpDown ? 1 : 0);
            if (Layout == NavLayout.Horizontal) dy = 0;
            if (Layout == NavLayout.Vertical) dx = 0;
            if (dx != 0 || dy != 0) MoveSelection(dx, dy, cols);
            if (GameInput.MenuConfirmDown && NavIndex >= 0) Nav[NavIndex].Click();
        }

        void MoveSelection(int dx, int dy, int cols)
        {
            int n = Nav.Count;
            int i = NavIndex < 0 ? 0 : NavIndex;
            for (int step = 0; step < n; step++)
            {
                if (dx != 0) i = (i + dx + n) % n;
                else
                {
                    int next = i + dy * cols;
                    if (next < 0 || next >= n) return; // 격자 끝에선 멈춘다
                    i = next;
                }
                if (Nav[i].Interactable && Nav[i].gameObject.activeInHierarchy) break;
            }
            SelectNav(i);
        }
    }
}
