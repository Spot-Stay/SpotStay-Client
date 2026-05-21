using System;

namespace 코빚노_프로젝트
{
    public class ChatRoomResponse
    {
        public int ChatRoomId { get; set; }
        public int RoomId { get; set; }

        public string RoomName { get; set; }
        public DateTime? CreatedAt { get; set; }

        public int GetRoomId()
        {
            if (ChatRoomId > 0)
                return ChatRoomId;

            return RoomId;
        }
    }

    public class ChatMessageRequest
    {
        public int ChatRoomId { get; set; }

        // 서버가 요구하는 이름
        public int SenderId { get; set; }

        // 기존 코드 방어용
        public int MemberId { get; set; }

        // 서버 모델은 Message
        public string Message { get; set; }

        // 기존 클라이언트 방어용
        public string Content { get; set; }
    }

    public class ChatMessageResponse
    {
        public int ChatMessageId { get; set; }
        public int MessageId { get; set; }

        public int ChatRoomId { get; set; }

        public int SenderId { get; set; }
        public int MemberId { get; set; }

        public string SenderName { get; set; }
        public string UserId { get; set; }
        public string MemberName { get; set; }

        public string MessageType { get; set; }

        // 서버 모델
        public string Message { get; set; }

        // 기존 코드 방어용
        public string Content { get; set; }

        public int? SpotId { get; set; }
        public string SpotName { get; set; }
        public string SpotAddress { get; set; }
        public string SpotImageUrl { get; set; }

        // 예전 DTO 방어용
        public string TargetType { get; set; }
        public int? TargetId { get; set; }
        public string TargetName { get; set; }
        public string TouristSpotName { get; set; }
        public string PlaceName { get; set; }
        public string Address { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public int? Rating { get; set; }
        public string RatingText { get; set; }
        public string ReviewCountText { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

    public class ChatMemberResponse
    {
        public int MemberId { get; set; }
        public int Id { get; set; }

        public string UserId { get; set; }
        public string MemberIdText { get; set; }
        public string Name { get; set; }
        public string MemberName { get; set; }
    }
}