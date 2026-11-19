using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 챕터·스테이지 구조(2026-11-19 사용자 확정, `docs/chapter-stage-spec.md`) — 챕터 5개 × (일반 스테이지 5 + 보스 스테이지 1).
    /// 예전 "지역(region) 9개 일렬"을 대신한다. **챕터 번호 = 예전 지역 번호**로 넘겨서 지역 기준 시스템(출현표·보스 목록·하늘색·회귀 장벽)을 그대로 쓴다.
    ///
    /// - 스테이지 번호 1~5 = 일반, <see cref="BossStage"/>(6) = 보스 스테이지.
    /// - 챕터 안에서 1 → 2 → 3 → 4 → 5 → 보스 순서로 열리고, 보스를 잡으면 다음 챕터가 열린다.
    /// - 지금 들어갈 수 있는 건 챕터 1~<see cref="PlayableChapters"/>(챕터 3~5는 보스가 없어 잠금 — 사용자).
    /// - 난이도 지수(GPT와 정함) T = (챕터-1) + (스테이지-1)/5 — 1-5 → 2-1이 매끄럽게 이어진다.
    /// </summary>
    public static class ChapterConfig
    {
        public const int ChapterCount = 5;
        public const int StagesPerChapter = 5;
        public const int BossStage = StagesPerChapter + 1;
        public const int StageCount = ChapterCount * StagesPerChapter;
        /// <summary>지금 들어갈 수 있는 챕터 수 — 3~5는 전용 보스가 생기면 연다.</summary>
        public const int PlayableChapters = 2;
        /// <summary>일반 스테이지 클리어 처치 수 — 여러 판 누적, 스테이지끼리 따로.</summary>
        public const int StageKillTarget = 100;

        public static readonly string[] Names = { "스산한 들판", "부서진 성역", "멎은 시계숲", "되감긴 심연", "시간의 마왕성" };

        public static string NameOf(int chapter) => chapter >= 1 && chapter <= ChapterCount ? Names[chapter - 1] : "?";

        /// <summary>"1-3"·"1-보스".</summary>
        public static string StageLabel(int chapter, int stage) => stage == BossStage ? $"{chapter}-보스" : $"{chapter}-{stage}";

        public static bool IsPlayable(int chapter) => chapter >= 1 && chapter <= PlayableChapters;

        /// <summary>스테이지 기록 배열 칸 — (챕터-1)×5 + (스테이지-1). 보스 스테이지는 -1(기록은 regionBossCleared).</summary>
        public static int Index(int chapter, int stage) =>
            chapter >= 1 && chapter <= ChapterCount && stage >= 1 && stage <= StagesPerChapter
                ? (chapter - 1) * StagesPerChapter + (stage - 1) : -1;

        // ── 난이도(GPT와 정함, `docs/chapter-stage-spec.md`) ──

        /// <summary>챕터 안 스테이지 단계(0~0.8) — 지역 배율의 지수에 더한다. 보스 스테이지는 다음 챕터 1스테이지와 같은 1.0.</summary>
        public static float StageStep(int stage) => stage == BossStage ? 1f : Mathf.Clamp(stage - 1, 0, StagesPerChapter - 1) / (float)StagesPerChapter;

        /// <summary>스테이지별 몹 레벨 추가값(1~5: 0,1,1,2,2 / 보스 +3).</summary>
        public static int StageLevelBonus(int stage) => stage switch { 2 => 1, 3 => 1, 4 => 2, 5 => 2, BossStage => 3, _ => 0 };

        public static float HpStageMult(int stage) => Mathf.Pow(DifficultyScalingConfig.HpPerRegion, StageStep(stage));
        public static float DmgStageMult(int stage) => Mathf.Pow(DifficultyScalingConfig.DmgPerRegion, StageStep(stage));
        public static float RewardStageMult(int stage) => Mathf.Pow(DifficultyScalingConfig.RewardGrow, StageStep(stage));

        // ── 챕터 보스(보스 스테이지) — 기존 보스 수치에 한 단계(1/5) 더(GPT) ──
        public static float BossHpMult => Mathf.Pow(DifficultyScalingConfig.HpPerRegion, 0.2f);
        public static float BossDmgMult => Mathf.Pow(DifficultyScalingConfig.DmgPerRegion, 0.2f);
        public static float BossRewardMult => Mathf.Pow(DifficultyScalingConfig.RewardGrow, 0.2f);

        // ── 중간 보스(5스테이지, 사용자 확정 + GPT 보상) ──
        public const float MidBossScale = 2f, MidBossHpMult = 20f, MidBossDmgMult = 5f, MidBossRewardMult = 10f;

        // ── 시간의 파편(원본 `rpOfRegion` — 이번 생에 잡은 챕터 보스마다 한 번) ──
        public static int ShardsOfChapter(int chapter) => RegressionConfig.ShardsOfRegion(chapter);
    }
}
