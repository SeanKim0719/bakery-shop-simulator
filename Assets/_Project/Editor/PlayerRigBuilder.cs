using BakeryShop.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BakeryShop.EditorTools
{
    /// <summary>씬 빌더가 플레이어를 만들고 받는 참조들.</summary>
    internal readonly struct PlayerRig
    {
        public readonly GameObject Root;
        public readonly PlayerInputReader Input;
        public readonly Interactor Interactor;

        public PlayerRig(GameObject root, PlayerInputReader input, Interactor interactor)
        {
            Root = root;
            Input = input;
            Interactor = interactor;
        }
    }

    /// <summary>
    /// M1의 플레이어(이동, 시점, 손, 상호작용)를 만들고 컴포넌트를 서로 연결한다.
    /// 그레이박스 씬과 매장 씬이 같은 플레이어를 쓰게 하려고 한 곳에 모았다.
    /// </summary>
    internal static class PlayerRigBuilder
    {
        /// <param name="yawDegrees">처음 바라보는 방향. 0이 북쪽(+z)이다.</param>
        public static PlayerRig Build(string name, InputActionAsset actions, Vector3 position, float yawDegrees)
        {
            var player = new GameObject(name);
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            var input = player.AddComponent<PlayerInputReader>();
            var motor = player.AddComponent<PlayerMotor>();
            var hands = player.AddComponent<Hands>();
            var interactor = player.AddComponent<Interactor>();

            Transform pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;

            Transform holdAnchor = new GameObject("HoldAnchor").transform;
            holdAnchor.SetParent(cameraObject.transform, false);
            holdAnchor.localPosition = new Vector3(0f, -0.3f, 0.9f);

            SceneBuildUtility.SetReference(input, "_actions", actions);
            SceneBuildUtility.SetReference(motor, "_input", input);
            SceneBuildUtility.SetReference(motor, "_cameraPivot", pivot);
            SceneBuildUtility.SetReference(hands, "_holdAnchor", holdAnchor);
            SceneBuildUtility.SetReference(interactor, "_input", input);
            SceneBuildUtility.SetReference(interactor, "_hands", hands);
            SceneBuildUtility.SetReference(interactor, "_motor", motor);
            SceneBuildUtility.SetReference(interactor, "_viewOrigin", cameraObject.transform);

            return new PlayerRig(player, input, interactor);
        }
    }
}
