using System;
using BakeryShop.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace BakeryShop.Tests
{
    public class InteractionResolverTests
    {
        private sealed class FakeHands : IHands
        {
            public IHoldable Held { get; set; }

            public bool TryPickUp(IHoldable item)
            {
                if (Held != null)
                {
                    return false;
                }

                Held = item;
                return true;
            }
        }

        private sealed class FakeHoldable : IHoldable
        {
            public HoldableKind Kind => HoldableKind.Box;
            public string DisplayName => "박스";
            public Vector3 HalfExtents => Vector3.one * 0.25f;
            public void OnPickedUp(Transform holdAnchor) { }
            public void OnDropped(Vector3 position, Quaternion rotation) { }
        }

        private sealed class FakeTarget : IInteractable
        {
            private readonly Func<InteractionContext, string> _prompt;

            public FakeTarget(Func<InteractionContext, string> prompt)
            {
                _prompt = prompt;
            }

            public string GetPrompt(in InteractionContext context) => _prompt(context);

            public void Interact(in InteractionContext context) { }
        }

        private static InteractionContext EmptyHands() => new InteractionContext(new FakeHands());

        private static InteractionContext Holding() => new InteractionContext(new FakeHands { Held = new FakeHoldable() });

        [Test]
        public void NoTarget_EmptyHands_ReturnsNone()
        {
            InteractionOption option = InteractionResolver.Resolve(null, EmptyHands(), canPlaceHere: true);

            Assert.AreEqual(InteractionKind.None, option.Kind);
        }

        [Test]
        public void TargetWithPrompt_ReturnsTargetAndItsPrompt()
        {
            var target = new FakeTarget(_ => "박스 들기");

            InteractionOption option = InteractionResolver.Resolve(target, EmptyHands(), canPlaceHere: false);

            Assert.AreEqual(InteractionKind.Target, option.Kind);
            Assert.AreEqual("박스 들기", option.Prompt);
            Assert.AreSame(target, option.Target);
        }

        [TestCase(null)]
        [TestCase("")]
        public void TargetWithoutPrompt_EmptyHands_ReturnsNone(string prompt)
        {
            var target = new FakeTarget(_ => prompt);

            InteractionOption option = InteractionResolver.Resolve(target, EmptyHands(), canPlaceHere: true);

            Assert.AreEqual(InteractionKind.None, option.Kind);
        }

        [Test]
        public void TargetPrompt_WinsOverPlacement()
        {
            var target = new FakeTarget(_ => "오븐 열기");

            InteractionOption option = InteractionResolver.Resolve(target, Holding(), canPlaceHere: true);

            Assert.AreEqual(InteractionKind.Target, option.Kind);
        }

        [Test]
        public void Holding_TargetHasNothingToDo_CanPlace_ReturnsPlace()
        {
            // 박스를 든 채 다른 박스 윗면을 보는 경우: 박스는 빈손일 때만 들 수 있으므로 쌓기가 된다.
            var boxOnFloor = new FakeTarget(ctx => ctx.Held == null ? "박스 들기" : null);

            InteractionOption option = InteractionResolver.Resolve(boxOnFloor, Holding(), canPlaceHere: true);

            Assert.AreEqual(InteractionKind.Place, option.Kind);
            Assert.AreEqual(InteractionResolver.PlacePrompt, option.Prompt);
        }

        [Test]
        public void Holding_NoTarget_CanPlace_ReturnsPlace()
        {
            InteractionOption option = InteractionResolver.Resolve(null, Holding(), canPlaceHere: true);

            Assert.AreEqual(InteractionKind.Place, option.Kind);
        }

        [Test]
        public void Holding_CannotPlace_ReturnsNone()
        {
            InteractionOption option = InteractionResolver.Resolve(null, Holding(), canPlaceHere: false);

            Assert.AreEqual(InteractionKind.None, option.Kind);
        }

        [Test]
        public void TargetReceivesCurrentHandState()
        {
            InteractionContext seen = default;
            var target = new FakeTarget(ctx =>
            {
                seen = ctx;
                return null;
            });
            InteractionContext holding = Holding();

            InteractionResolver.Resolve(target, holding, canPlaceHere: false);

            Assert.AreSame(holding.Held, seen.Held);
        }

        [Test]
        public void Options_WithSameKindPromptAndTarget_AreEqual()
        {
            var target = new FakeTarget(_ => "들기");

            Assert.AreEqual(InteractionOption.ForTarget(target, "들기"), InteractionOption.ForTarget(target, "들기"));
            Assert.AreNotEqual(InteractionOption.ForTarget(target, "들기"), InteractionOption.ForTarget(new FakeTarget(_ => "들기"), "들기"));
            Assert.AreNotEqual(InteractionOption.ForPlace("내려놓기"), InteractionOption.None);
        }
    }
}
