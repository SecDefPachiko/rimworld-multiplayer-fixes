namespace Multiplayer.Common.Networking.Packet;

[PacketDefinition(Packets.Server_Cursor)]
public record struct ServerCursorPacket(int playerId, ClientCursorPacket data) : IPacket
{
    public int playerId = playerId;
    public ClientCursorPacket data = data;

    public void Bind(PacketBuffer buf)
    {
        buf.Bind(ref playerId);
        buf.Bind(ref data);
    }
}

[PacketDefinition(Packets.Client_Cursor)]
public record struct ClientCursorPacket(byte seq) : IPacket
{
    public const byte FlagDrag = 1, FlagPlacing = 2, FlagDesignationDrag = 4;

    public byte seq = seq;
    /// byte.MaxValue signifies an absence of a specific map (e.g., when viewing the world).
    public byte map = byte.MaxValue;
    public byte icon;
    public float x, z;
    /// Which of the optional parts below are present
    public byte flags;

    /// Selection drag box start
    public float dragX, dragZ;

    /// What a build/install designator is placing: def short hash, whether it's a TerrainDef, rotation (Rot4.AsInt)
    public ushort placingDefHash;
    public bool placingTerrain;
    public byte placingRot;

    /// Designation drag (walls, zones, mining...): start cell and the DrawStyleDef short hash that shapes the dragged cells
    public short designationDragX, designationDragZ;
    public ushort designationDragStyleHash;

    public bool HasDrag => (flags & FlagDrag) != 0;
    public bool HasPlacing => (flags & FlagPlacing) != 0;
    public bool HasDesignationDrag => (flags & FlagDesignationDrag) != 0;

    public void Bind(PacketBuffer buf)
    {
        buf.Bind(ref seq);
        buf.Bind(ref map);
        if (map < byte.MaxValue)
        {
            buf.Bind(ref icon);
            buf.BindWith(ref x, FloatAsShortBinder);
            buf.BindWith(ref z, FloatAsShortBinder);
            buf.Bind(ref flags);

            if (HasDrag)
            {
                buf.BindWith(ref dragX, FloatAsShortBinder);
                buf.BindWith(ref dragZ, FloatAsShortBinder);
            }

            if (HasPlacing)
            {
                buf.Bind(ref placingDefHash);
                buf.Bind(ref placingTerrain);
                buf.Bind(ref placingRot);
            }

            if (HasDesignationDrag)
            {
                buf.Bind(ref designationDragX);
                buf.Bind(ref designationDragZ);
                buf.Bind(ref designationDragStyleHash);
            }
        }
    }

    private static readonly Binder<float> FloatAsShortBinder = (PacketBuffer buf, ref float f) =>
    {
        if (buf is PacketWriter { Writer: var writer }) writer.WriteShort((short)(f * 10f));
        else if (buf is PacketReader { Reader: var reader }) f = reader.ReadShort() / 10f;
    };
}
