using System;
using AOT;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Burst;

// デフォルトワールドでの制御システムの更新
[UpdateInWorld(UpdateInWorld.TargetWorld.Default)]
public class Game : ComponentSystem
{
    //　Gameを一度だけ実行するフラグ
    struct InitGameComponent: IComponentData
    {

    }

    //生成時に呼ばれる
    protected override void OnCreate()
    {
        // InitGameComponentを持つ場合のみGameを有効化
        RequireSingletonForUpdate<InitGameComponent>();

        //InitGameComponentを持つEntityを作成しGameを実行
        EntityManager.CreateEntity(typeof(InitGameComponent));

    }

    protected override void OnUpdate()
    {
        // InitGameComponentを破棄して　Gameが再度実行されないように
        EntityManager.DestroyEntity(GetSingletonEntity<InitGameComponent>());

        //ワールドの反復
        foreach(var world in World.All)
        {
            var network = world.GetExistingSystem<NetworkStreamReceiveSystem>();
            if(world.GetExistingSystem<ClientSimulationSystemGroup>() != null)
            {
                //クライアントワールドはローカルホストにあるサーバーに接続
                NetworkEndPoint ep = NetworkEndPoint.LoopbackIpv4;

                ep.Port = 7979;
#if UNITY_EDITOR
                ep = NetworkEndPoint.Parse(ClientServerBootstrap.RequestedAutoConnect, 7979);
#endif
                network.Connect(ep);

            }
#if UNITY_EDITOR || UNITY_SERVER
            else if (world.GetExistingSystem<ServerSimulationSystemGroup>() != null)
            {
                // サーバーワールドはクライアント接続のリッスンを開始
                NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                ep.Port = 7979;
                network.Listen(ep);
            }
#endif

        }

    }

    //---------------------------------------------------------------------------------------------
    // ゲーム参加リクエスト
    [BurstCompile]
    public struct GoInGameRequest : IRpcCommand
    {
        // デモ用の未使用の整数
        public int value;

        // デシリアライズ
        public void Deserialize(ref DataStreamReader reader)
        {
            value = reader.ReadInt();
        }

        // シリアライズ
        public void Serialize(ref DataStreamWriter writer)
        {
            writer.WriteInt(value);
        }

        // 呼び出し実行
        [BurstCompile]
        [MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            // RPCリクエストの生成
            RpcExecutor.ExecuteCreateRequestComponent<GoInGameRequest>(ref parameters);
        }

        // 呼び出し実行の関数ポインタの取得
        static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }
    }

    //--------------------------------------------------------------------------------------------------------


    //--------------------------------------------------------------------------------------------------------
    // ゲーム参加リクエストの転送システム
    public class GoInGameRequestSystem : RpcCommandRequestSystem<GoInGameRequest>
    {
    }

    //-------------------------------------------------------------------------------------------------------



    //-------------------------------------------------------------------------------------------------------
    // ゲーム参加リクエストの送信システム
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public class GoInGameClientSystem : ComponentSystem
    {
        // 生成時に呼ばれる
        protected override void OnCreate()
        {
            // GoInGameClientSystemを持つ場合のみGoInGameClientSystemを有効化
            RequireSingletonForUpdate<EnableNetCodeTestGhostReceiveSystemComponent>();
        }

        // 1フレーム毎に呼ばれる
        protected override void OnUpdate()
        {
            Entities.WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
            {
                // ゲーム参加リクエストの送信
                PostUpdateCommands.AddComponent<NetworkStreamInGame>(ent);
                var req = PostUpdateCommands.CreateEntity();
                PostUpdateCommands.AddComponent<GoInGameRequest>(req);
                PostUpdateCommands.AddComponent(req, new SendRpcCommandRequestComponent
                {
                    TargetConnection = ent
                });
            });
        }
    }
    //--------------------------------------------------------------------------------------------------------


    //--------------------------------------------------------------------------------------------------------

    // ゲーム参加リクエストの受信システム
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public class GoInGameServerSystem : ComponentSystem
    {
        // 生成時に呼ばれる
        protected override void OnCreate()
        {
            // EnableNetCodeTestGhostSendSystemComponentがある場合のみSystem動作
            RequireSingletonForUpdate<EnableNetCodeTestGhostSendSystemComponent>();
        }

        // 更新時に呼ばれる
        protected override void OnUpdate()
        {
            Entities.WithNone<SendRpcCommandRequestComponent>().ForEach((Entity reqEnt, ref GoInGameRequest req, ref ReceiveRpcCommandRequestComponent reqSrc) =>
            {
                // ゲーム参加リクエストの受信
                PostUpdateCommands.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
                UnityEngine.Debug.Log(String.Format("Server setting connection {0} to in game", EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value));
#if true
                // Cubeの生成
                var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();
                var ghostId = NetCodeTestGhostSerializerCollection.FindGhostType<CubeSnapshotData>();
                var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[ghostId].Value;
                var player = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(player, new MovableCubeComponent
                {
                    PlayerId = EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value
                });
                PostUpdateCommands.AddBuffer<CubeInput>(player);
                PostUpdateCommands.SetComponent(reqSrc.SourceConnection, new CommandTargetComponent { targetEntity = player });
#endif

                // ゲーム参加リクエストの破棄
                PostUpdateCommands.DestroyEntity(reqEnt);
            });
        }
    }
    //----------------------------------------------------------------------------------------------------------
}
