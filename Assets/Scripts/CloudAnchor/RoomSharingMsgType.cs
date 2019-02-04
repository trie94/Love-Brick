namespace Love.Core
{
    using UnityEngine.Networking;

    /// <summary>
    /// Room Sharing Message Types.
    /// </summary>
    public struct RoomSharingMsgType
    {
        /// <summary>
        /// The Anchor id from room request message type.
        /// </summary>
        public const short AnchorIdFromRoomRequest = MsgType.Highest + 1;

        /// <summary>
        /// The Anchor id from room response message type.
        /// </summary>
        public const short AnchorIdFromRoomResponse = MsgType.Highest + 2;

        // network transform anchor message type
        public const short NetworkTransformAnchor = MsgType.Highest + 3;

        // client sync var
        public const short NetworkClientSyncLocal = MsgType.Highest + 4;
    }
}
