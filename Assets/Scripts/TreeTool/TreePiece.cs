using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Category a tree piece belongs to. Drives which pool the builder pulls it from
/// and the socket layout it is expected to have.
/// </summary>
public enum TreePieceType
{
    Base,
    Trunk,
    Split,
    Branch,
    Top
}

/// <summary>
/// Whether a socket receives an incoming connection (In) or hands off to the next
/// piece / branch (Out).
/// </summary>
public enum TreeSocketRole
{
    In,
    Out
}

/// <summary>
/// A two-point connection seam authored in the piece's local space. The segment
/// from <see cref="a"/> to <see cref="b"/> defines both the position and the
/// orientation (and, when scaling is enabled, the width) of the overlap where two
/// pieces join.
/// </summary>
[System.Serializable]
public class TreeSocket
{
    [Tooltip("Label shown on gizmos/handles. Purely for authoring clarity.")]
    public string name = "Socket";

    [Tooltip("In sockets receive an incoming piece; Out sockets feed the next piece or a branch.")]
    public TreeSocketRole role = TreeSocketRole.Out;

    [Tooltip("Out sockets flagged as branch points are the only places a branch stroke can attach.")]
    public bool isBranchPoint = false;

    [Tooltip("First seam endpoint, in the piece's local space.")]
    public Vector2 a = new Vector2(-0.5f, 0f);

    [Tooltip("Second seam endpoint, in the piece's local space. Direction from a to b sets orientation.")]
    public Vector2 b = new Vector2(0.5f, 0f);

    public Vector2 LocalMidpoint => (a + b) * 0.5f;
}

/// <summary>
/// Authoring component placed on every modular tree prefab (base, trunk, split,
/// branch, top). Stores the connection sockets used by the Tree Builder tool to
/// snap pieces together. Lives in the runtime assembly so the data persists on
/// prefabs; all editing UI lives in the editor-only <c>TreePieceEditor</c>.
/// </summary>
[DisallowMultipleComponent]
public class TreePiece : MonoBehaviour
{
    [Header("Piece")]
    [SerializeField] private TreePieceType pieceType = TreePieceType.Trunk;

    [Header("Sockets")]
    [Tooltip("Connection seams for this piece. Typical layouts: Base = 1 Out; Trunk = 1 In + 1 Out; " +
             "Split = 1 In + 2 Out (one flagged as a branch point); Branch = 1 In + 1 Out; Top = 1 In.")]
    [SerializeField] private List<TreeSocket> sockets = new List<TreeSocket>();

    public TreePieceType PieceType => pieceType;
    public List<TreeSocket> Sockets => sockets;

    /// <summary>First In socket on the piece, or null if it has none (e.g. a Base).</summary>
    public TreeSocket GetInSocket()
    {
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].role == TreeSocketRole.In)
                return sockets[i];
        }
        return null;
    }

    /// <summary>All Out sockets on the piece (continuations and branch points).</summary>
    public IEnumerable<TreeSocket> GetOutSockets()
    {
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].role == TreeSocketRole.Out)
                yield return sockets[i];
        }
    }

    /// <summary>The main continuation Out socket: the first non-branch-point Out, else the first Out.</summary>
    public TreeSocket GetMainOutSocket()
    {
        TreeSocket firstOut = null;
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].role != TreeSocketRole.Out)
                continue;
            if (firstOut == null)
                firstOut = sockets[i];
            if (!sockets[i].isBranchPoint)
                return sockets[i];
        }
        return firstOut;
    }

    public Vector3 WorldPoint(Vector2 localPoint)
    {
        return transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
    }

    public Vector3 WorldPointA(TreeSocket socket) => WorldPoint(socket.a);
    public Vector3 WorldPointB(TreeSocket socket) => WorldPoint(socket.b);
    public Vector3 WorldMidpoint(TreeSocket socket) => WorldPoint(socket.LocalMidpoint);

    private void OnDrawGizmos()
    {
        if (sockets == null)
            return;

        for (int i = 0; i < sockets.Count; i++)
        {
            TreeSocket socket = sockets[i];
            if (socket == null)
                continue;

            Vector3 worldA = WorldPointA(socket);
            Vector3 worldB = WorldPointB(socket);

            if (socket.role == TreeSocketRole.In)
                Gizmos.color = new Color(0.2f, 0.7f, 1f);            // In: blue
            else if (socket.isBranchPoint)
                Gizmos.color = new Color(1f, 0.55f, 0.1f);           // Branch point: orange
            else
                Gizmos.color = new Color(0.3f, 1f, 0.3f);            // Out: green

            Gizmos.DrawLine(worldA, worldB);

            float radius = HandleRadius(worldA, worldB);
            Gizmos.DrawSphere(worldA, radius);
            Gizmos.DrawSphere(worldB, radius);
        }
    }

    private static float HandleRadius(Vector3 worldA, Vector3 worldB)
    {
        float length = Vector3.Distance(worldA, worldB);
        return Mathf.Max(0.05f, length * 0.08f);
    }
}
