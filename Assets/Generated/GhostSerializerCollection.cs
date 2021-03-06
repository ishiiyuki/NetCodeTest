using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode;

public struct NetCodeTestGhostSerializerCollection : IGhostSerializerCollection
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string[] CreateSerializerNameList()
    {
        var arr = new string[]
        {
            "CubeGhostSerializer",
        };
        return arr;
    }

    public int Length => 1;
#endif
    public static int FindGhostType<T>()
        where T : struct, ISnapshotData<T>
    {
        if (typeof(T) == typeof(CubeSnapshotData))
            return 0;
        return -1;
    }

    public void BeginSerialize(ComponentSystemBase system)
    {
        m_CubeGhostSerializer.BeginSerialize(system);
    }

    public int CalculateImportance(int serializer, ArchetypeChunk chunk)
    {
        switch (serializer)
        {
            case 0:
                return m_CubeGhostSerializer.CalculateImportance(chunk);
        }

        throw new ArgumentException("Invalid serializer type");
    }

    public int GetSnapshotSize(int serializer)
    {
        switch (serializer)
        {
            case 0:
                return m_CubeGhostSerializer.SnapshotSize;
        }

        throw new ArgumentException("Invalid serializer type");
    }

    public int Serialize(ref DataStreamWriter dataStream, SerializeData data)
    {
        switch (data.ghostType)
        {
            case 0:
            {
                return GhostSendSystem<NetCodeTestGhostSerializerCollection>.InvokeSerialize<CubeGhostSerializer, CubeSnapshotData>(m_CubeGhostSerializer, ref dataStream, data);
            }
            default:
                throw new ArgumentException("Invalid serializer type");
        }
    }
    private CubeGhostSerializer m_CubeGhostSerializer;
}

public struct EnableNetCodeTestGhostSendSystemComponent : IComponentData
{}
public class NetCodeTestGhostSendSystem : GhostSendSystem<NetCodeTestGhostSerializerCollection>
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<EnableNetCodeTestGhostSendSystemComponent>();
    }

    public override bool IsEnabled()
    {
        return HasSingleton<EnableNetCodeTestGhostSendSystemComponent>();
    }
}
