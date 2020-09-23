using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

// キー入力データ
public struct CubeInput : ICommandData<CubeInput>
{
    public uint Tick => tick;
    public uint tick;
    public int horizontal;
    public int vertical;

    // デシリアライズ
    public void Deserialize(uint tick, ref DataStreamReader reader)
    {
        this.tick = tick;
        horizontal = reader.ReadInt();
        vertical = reader.ReadInt();
    }

    // シリアライズ
    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteInt(horizontal);
        writer.WriteInt(vertical);
    }

    // ディリアライズ
    public void Deserialize(uint tick, ref DataStreamReader reader, CubeInput baseline,
        NetworkCompressionModel compressionModel)
    {
        Deserialize(tick, ref reader);
    }

    // シリアライズ
    public void Serialize(ref DataStreamWriter writer, CubeInput baseline, NetworkCompressionModel compressionModel)
    {
        Serialize(ref writer);
    }

    // コマンド送信システム
    public class NetCodeTestSendCommandSystem : CommandSendSystem<CubeInput>
    {
    }

    // コマンド受信システム
    public class NetCodeTestReceiveCommandSystem : CommandReceiveSystem<CubeInput>
    {
    }





    // キー入力データのサンプリング
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public class SampleCubeInput : ComponentSystem
    {
        // 生成時に呼ばれる
        protected override void OnCreate()
        {
            RequireSingletonForUpdate<NetworkIdComponent>();

            // GoInGameClientSystemを持つ場合のみSampleCubeInputを有効化
            RequireSingletonForUpdate<EnableNetCodeTestGhostReceiveSystemComponent>();
        }

        // 1フレーム毎に呼ばれる
        protected override void OnUpdate()
        {
            // localInputの取得
            var localInput = GetSingleton<CommandTargetComponent>().targetEntity;
            if (localInput == Entity.Null)
            {
                var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
                Entities.WithNone<CubeInput>().ForEach((Entity ent, ref MovableCubeComponent cube) =>
                {
                    if (cube.PlayerId == localPlayerId)
                    {
                        PostUpdateCommands.AddBuffer<CubeInput>(ent);
                        PostUpdateCommands.SetComponent(GetSingletonEntity<CommandTargetComponent>(), new CommandTargetComponent { targetEntity = ent });
                    }
                });
                return;
            }

            // キー入力データの生成
            var input = default(CubeInput);
            input.tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;
            if (Input.GetKey("a"))
                input.horizontal -= 1;
            if (Input.GetKey("d"))
                input.horizontal += 1;
            if (Input.GetKey("s"))
                input.vertical -= 1;
            if (Input.GetKey("w"))
                input.vertical += 1;

            // サーバーに送信
            var inputBuffer = EntityManager.GetBuffer<CubeInput>(localInput);
            inputBuffer.AddCommandData(input);
        }
    }
}