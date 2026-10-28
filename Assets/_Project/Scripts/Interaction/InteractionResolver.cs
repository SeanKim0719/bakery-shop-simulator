using System;

namespace BakeryShop.Interaction
{
    public enum InteractionKind
    {
        /// <summary>할 수 있는 게 없음.</summary>
        None,

        /// <summary>바라보는 대상과 상호작용 (들기, 열기, 굽기 등).</summary>
        Target,

        /// <summary>손에 든 물건을 바라보는 바닥·윗면에 내려놓기.</summary>
        Place,
    }

    /// <summary>이번 프레임에 상호작용 키를 누르면 일어날 일. HUD 안내와 실제 실행이 같은 값을 본다.</summary>
    public readonly struct InteractionOption : IEquatable<InteractionOption>
    {
        public static readonly InteractionOption None = default;

        public readonly InteractionKind Kind;
        public readonly string Prompt;
        public readonly IInteractable Target;

        private InteractionOption(InteractionKind kind, string prompt, IInteractable target)
        {
            Kind = kind;
            Prompt = prompt;
            Target = target;
        }

        public static InteractionOption ForTarget(IInteractable target, string prompt) =>
            new InteractionOption(InteractionKind.Target, prompt, target);

        public static InteractionOption ForPlace(string prompt) =>
            new InteractionOption(InteractionKind.Place, prompt, null);

        public bool Equals(InteractionOption other) =>
            Kind == other.Kind && Prompt == other.Prompt && ReferenceEquals(Target, other.Target);

        public override bool Equals(object obj) => obj is InteractionOption other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Kind, Prompt);

        public static bool operator ==(InteractionOption left, InteractionOption right) => left.Equals(right);

        public static bool operator !=(InteractionOption left, InteractionOption right) => !left.Equals(right);
    }

    /// <summary>
    /// 바라보는 대상과 손 상태로 "지금 E를 누르면 무엇을 하는지"를 정한다.
    /// Raycast 같은 엔진 기능과 분리해 두어 EditMode 테스트로 규칙을 검증한다.
    /// </summary>
    public static class InteractionResolver
    {
        public const string PlacePrompt = "내려놓기";

        /// <param name="target">바라보는 상호작용 대상. 없으면 null.</param>
        /// <param name="context">손 상태.</param>
        /// <param name="canPlaceHere">바라보는 곳이 손에 든 물건을 내려놓을 수 있는 빈 윗면인지.</param>
        public static InteractionOption Resolve(IInteractable target, in InteractionContext context, bool canPlaceHere)
        {
            // 대상이 할 수 있는 행동을 내놓으면 그것이 우선이다.
            if (target != null)
            {
                string prompt = target.GetPrompt(context);
                if (!string.IsNullOrEmpty(prompt))
                {
                    return InteractionOption.ForTarget(target, prompt);
                }
            }

            // 대상이 없거나 대상이 할 일이 없으면, 들고 있는 물건을 그 자리에 내려놓는다.
            // 예: 박스를 든 채 다른 박스 윗면을 보면 쌓기가 된다.
            if (context.Held != null && canPlaceHere)
            {
                return InteractionOption.ForPlace(PlacePrompt);
            }

            return InteractionOption.None;
        }
    }
}
