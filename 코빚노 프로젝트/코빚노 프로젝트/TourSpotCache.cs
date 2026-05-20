using System.Collections.Generic;

namespace 코빚노_프로젝트
{
    public static class TourSpotCache
    {
        private static readonly Dictionary<int, string> _nameByTargetId =
            new Dictionary<int, string>();

        public static void SaveTour(TourSpotItem tour)
        {
            if (tour == null)
                return;

            int targetId;

            if (!int.TryParse(tour.ContentId, out targetId))
                return;

            if (_nameByTargetId.ContainsKey(targetId))
            {
                _nameByTargetId[targetId] = tour.Name;
            }
            else
            {
                _nameByTargetId.Add(targetId, tour.Name);
            }
        }

        public static void SaveTours(List<TourSpotItem> tours)
        {
            if (tours == null)
                return;

            foreach (TourSpotItem tour in tours)
            {
                SaveTour(tour);
            }
        }

        public static string GetName(int targetId)
        {
            if (_nameByTargetId.ContainsKey(targetId))
                return _nameByTargetId[targetId];

            return "";
        }
    }
}