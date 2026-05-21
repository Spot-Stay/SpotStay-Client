using System;

namespace 코빚노_프로젝트
{
    public class ReviewResponse
    {
        public int ReviewId { get; set; }
        public int MemberId { get; set; }

        public string TargetType { get; set; }
        public int TargetId { get; set; }

        public int Rating { get; set; }
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string UserId { get; set; }
        public string MemberName { get; set; }
        public string TargetName { get; set; }
    }
}