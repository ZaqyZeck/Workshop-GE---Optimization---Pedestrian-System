//using Unity.Entities;
//using Unity.Mathematics;
//using UnityEngine.InputSystem;

//namespace TMG.Town
//{
//    [UpdateBefore(typeof(CharacterMoveSystem))]
//    public partial struct CharacterInputSystem : ISystem
//    {
//        public void OnUpdate(ref SystemState state)
//        {
//            if (Keyboard.current == null)
//                return;

//            float2 input = float2.zero;

//            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
//                input.y += 1;

//            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
//                input.y -= 1;

//            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
//                input.x += 1;

//            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
//                input.x -= 1;

//            input = math.normalizesafe(input);

//            foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>())
//            {
//                direction.ValueRW.value = input;
//            }
//        }
//    }
//}