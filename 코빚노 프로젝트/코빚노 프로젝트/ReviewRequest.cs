namespace 코빚노_프로젝트
{
    public class ReviewRequest
    {
        public int MemberId { get; set; }
        public string TargetType { get; set; }
        public int TargetId { get; set; }
        public int Rating { get; set; }
        public string Content { get; set; }
    }
}