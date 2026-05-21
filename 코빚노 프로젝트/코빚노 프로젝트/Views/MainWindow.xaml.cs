using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace 코빚노_프로젝트
{
    public partial class MainWindow : Window
    {
        private List<TourSpotItem> _tourList;
        private TourSpotItem _selectedTour;
        private readonly TourApiService _tourApiService = new TourApiService();
        private List<StayItem> _nearbyStayList = new List<StayItem>();

        private string _sortMode = "기본순";
        private string _selectedCategory = "전체";

        private bool _isMapLoaded = false;
        private bool _isLoggedIn = false;
        private string _loginUserId = "";

        private double _myMapX = 0; // 경도
        private double _myMapY = 0; // 위도
        private bool _hasMyLocation = false;

        // 거리순 기준: 우송대
        private const double WOOSONG_MAPY = 36.3376;
        private const double WOOSONG_MAPX = 127.4456;

        private int _loginMemberId = 1; // 테스트용. 로그인 API에서 MemberId 받으면 여기에 넣으면 됨

        private FriendWindow _friendWindow;
        private DateTime _accommodationBlockedUntil = DateTime.MinValue;
        private Dictionary<string, List<StayItem>> _stayPreviewCache = new Dictionary<string, List<StayItem>>();
        private DateTime _stayBlockedUntil = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            LoadKakaoMap();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CmbRegion.SelectedIndex = 2;
            TxtSearch.Text = "대전";

            SetMyLocationToWoosong();

            await LoadTourApiDataAsync("대전");
        }

        private void SetMyLocationToWoosong()
        {
            _myMapY = WOOSONG_MAPY;
            _myMapX = WOOSONG_MAPX;
            _hasMyLocation = true;
        }

        #region 관광지 선택 / 검색 / 카테고리

        private void SetSelectedTour(TourSpotItem item)
        {
            if (item == null)
                return;

            _selectedTour = item;

            TxtDetailIcon.Text = item.Icon;
            TxtDetailName.Text = item.Name;
            TxtDetailCategory.Text = item.Category;
            TxtDetailAddress.Text = "📍 " + item.Address;

            _ = MoveMapToTourAsync(item);
            _ = ShowTourImageOnMapAsync(item);
            _ = LoadNearbyStayPreviewAsync(item);
            _ = LoadReviewSummaryAsync(item);
            _ = LoadNearbyStayPreviewAsync(item);

            UpdateDetailFavoriteButton();
        }

        private void TourSpotCard_Click(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement fe = sender as FrameworkElement;

            if (fe == null)
                return;

            TourSpotItem item = fe.DataContext as TourSpotItem;

            if (item == null)
                return;

            if (TourSpotList.SelectedItem != item)
            {
                TourSpotList.SelectedItem = item;
            }
            else
            {
                SetSelectedTour(item);
            }
        }

        private void TourSpotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TourSpotItem item = TourSpotList.SelectedItem as TourSpotItem;

            if (item == null)
                return;

            SetSelectedTour(item);
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(keyword))
                keyword = GetSelectedRegionKeyword();

            _selectedCategory = "전체";
            SetAllCategoryButtonSelected();

            await LoadKeywordSearchAsync(keyword);
        }

        private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnSearch_Click(sender, e);
            }
        }

        private async void Keyword_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == null)
                return;

            SetSelectedCategoryButton(btn);

            _selectedCategory = NormalizeCategoryName(btn.Content == null ? "" : btn.Content.ToString());

            string region = GetSelectedRegionKeyword();

            TxtSearch.Text = region;

            if (_selectedCategory == "전체")
            {
                await LoadTourApiDataAsync(region);
            }
            else
            {
                await LoadCategorySearchAsync(_selectedCategory);
            }
        }

        private async void CmbRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _selectedCategory = "전체";
            SetAllCategoryButtonSelected();

            string keyword = GetSelectedRegionKeyword();

            TxtSearch.Text = keyword;

            await LoadTourApiDataAsync(keyword);
        }

        private async Task LoadTourApiDataAsync(string keyword)
        {
            try
            {
                TxtResultSub.Text = "관광공사 API 조회 중...";

                List<TourSpotItem> list =
                    await _tourApiService.SearchTourSpotsAsync(keyword, 100);

                if (list == null)
                    list = new List<TourSpotItem>();

                string regionFilter = GetSelectedRegionFilterText();

                if (!string.IsNullOrWhiteSpace(regionFilter))
                {
                    list = list
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.Address) &&
                            x.Address.Contains(regionFilter))
                        .ToList();
                }

                SetTourListResult(list, keyword + " · 검색 결과");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "관광공사 API 오류");
            }
        }

        private async Task LoadKeywordSearchAsync(string keyword)
        {
            try
            {
                TxtResultSub.Text = keyword + " 검색 중...";

                string regionKeyword = GetSelectedRegionKeyword();
                string regionFilter = GetSelectedRegionFilterText();

                // 카페/맛집/음식점 계열은 관광공사 말고 네이버 지역 API 사용
                if (IsNaverLocalKeyword(keyword))
                {
                    List<TourSpotItem> naverList;

                    if (regionKeyword == "대전")
                    {
                        naverList = await SearchNaverLocalManyAsync(
                            "우송대 " + keyword,
                            "대전 동구 " + keyword,
                            "대전 " + keyword
                        );
                    }
                    else
                    {
                        naverList = await SearchNaverLocalManyAsync(
                            regionKeyword + " " + keyword
                        );
                    }

                    if (!string.IsNullOrWhiteSpace(regionFilter))
                    {
                        naverList = naverList
                            .Where(x =>
                                !string.IsNullOrWhiteSpace(x.Address) &&
                                x.Address.Contains(regionFilter))
                            .ToList();
                    }

                    SetTourListResult(naverList, keyword + " · 네이버 지역 검색");
                    return;
                }

                // 아래는 기존 관광공사 API 로직 그대로
                List<TourSpotItem> keywordList =
                    await _tourApiService.SearchTourSpotsAsync(keyword, 100);

                if (keywordList == null)
                    keywordList = new List<TourSpotItem>();

                List<TourSpotItem> regionKeywordList = new List<TourSpotItem>();

                if (keyword != regionKeyword)
                {
                    regionKeywordList =
                        await _tourApiService.SearchTourSpotsAsync(regionKeyword + " " + keyword, 100);

                    if (regionKeywordList == null)
                        regionKeywordList = new List<TourSpotItem>();
                }

                List<TourSpotItem> regionList =
                    await _tourApiService.SearchTourSpotsAsync(regionKeyword, 100);

                if (regionList == null)
                    regionList = new List<TourSpotItem>();

                List<TourSpotItem> merged =
                    MergeDistinctTours(keywordList, regionKeywordList, regionList);

                if (!string.IsNullOrWhiteSpace(regionFilter))
                {
                    merged = merged
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.Address) &&
                            x.Address.Contains(regionFilter))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(keyword) &&
                    keyword != regionKeyword)
                {
                    merged = merged
                        .Where(x => IsMatchedSearchKeyword(x, keyword))
                        .ToList();
                }

                SetTourListResult(merged, keyword + " · 검색 결과");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "검색 오류");
            }
        }

        private async Task LoadCategorySearchAsync(string category)
        {
            try
            {
                category = NormalizeCategoryName(category);

                string regionKeyword = GetSelectedRegionKeyword();
                string regionFilter = GetSelectedRegionFilterText();

                TxtResultSub.Text = regionKeyword + " " + category + " 검색 중...";

                // 맛집은 네이버 지역 API 사용
                if (category == "맛집")
                {
                    List<TourSpotItem> naverList;

                    if (regionKeyword == "대전")
                    {
                        naverList = await SearchNaverLocalManyAsync(
                            "우송대 맛집",
                            "우송대 카페",
                            "대전 동구 맛집",
                            "대전 음식점",
                            "대전 카페"
                        );
                    }
                    else
                    {
                        naverList = await SearchNaverLocalManyAsync(
                            regionKeyword + " 맛집",
                            regionKeyword + " 음식점",
                            regionKeyword + " 카페"
                        );
                    }

                    if (!string.IsNullOrWhiteSpace(regionFilter))
                    {
                        naverList = naverList
                            .Where(x =>
                                !string.IsNullOrWhiteSpace(x.Address) &&
                                x.Address.Contains(regionFilter))
                            .ToList();
                    }

                    SetTourListResult(naverList, regionKeyword + " 맛집 · 네이버 지역 검색");
                    return;
                }

                List<TourSpotItem> merged = new List<TourSpotItem>();

                // 중요:
                // 카테고리 검색에서는 지역 전체 검색 결과를 섞지 않는다.
                // 지역 전체 결과를 먼저 AddRange 하면 쇼핑/음식점/골프장이 국립공원·문화유적에 섞인다.
                List<string> searchKeywords = GetCategorySearchKeywords(regionKeyword, category);

                foreach (string keyword in searchKeywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword))
                        continue;

                    List<TourSpotItem> list =
                        await _tourApiService.SearchTourSpotsAsync(keyword, 100);

                    if (list != null)
                        merged.AddRange(list);
                }

                merged = MergeDistinctTours(merged);

                // 지역 필터
                if (!string.IsNullOrWhiteSpace(regionFilter))
                {
                    merged = merged
                        .Where(x => IsMatchedRegionForCategory(x, regionFilter, category))
                        .ToList();
                }

                // 최종 카테고리 필터
                merged = merged
                    .Where(x => IsMatchedCategory(x, category))
                    .ToList();

                SetTourListResult(merged, regionKeyword + " " + category + " · 검색 결과");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "카테고리 검색 오류");
            }
        }

        private bool IsMatchedRegionForCategory(TourSpotItem item, string regionFilter, string category)
        {
            if (item == null)
                return false;

            if (string.IsNullOrWhiteSpace(regionFilter))
                return true;

            string name = item.Name ?? "";
            string address = item.Address ?? "";

            // 일반 관광지는 주소 기준으로 필터
            if (address.Contains(regionFilter))
                return true;

            // 국립공원은 행정구역 경계/대표주소 때문에 예외 처리
            if (category == "국립공원")
            {
                if (regionFilter == "대전")
                {
                    return name.Contains("계룡산") ||
                           name.Contains("수통골");
                }

                if (regionFilter == "제주")
                {
                    return name.Contains("한라산");
                }

                if (regionFilter == "강원")
                {
                    return name.Contains("설악산") ||
                           name.Contains("오대산") ||
                           name.Contains("치악산") ||
                           name.Contains("태백산");
                }

                if (regionFilter == "경상남도")
                {
                    return name.Contains("지리산") ||
                           name.Contains("가야산") ||
                           name.Contains("한려해상") ||
                           name.Contains("덕유산");
                }

                if (regionFilter == "전라남도")
                {
                    return name.Contains("지리산") ||
                           name.Contains("무등산") ||
                           name.Contains("월출산") ||
                           name.Contains("다도해") ||
                           name.Contains("한려해상");
                }
            }

            return false;
        }
        private List<string> GetCategorySearchKeywords(string regionKeyword, string category)
        {
            category = NormalizeCategoryName(category);

            List<string> keywords = new List<string>();

            if (category == "국립공원")
            {
                keywords.Add(regionKeyword + " 국립공원");

                if (regionKeyword == "서울")
                {
                    keywords.Add("북한산국립공원");
                    keywords.Add("국립공원 산악박물관");
                }
                else if (regionKeyword == "대전")
                {
                    keywords.Add("계룡산국립공원");
                    keywords.Add("계룡산");
                    keywords.Add("수통골");
                }
                else if (regionKeyword == "제주")
                {
                    keywords.Add("한라산국립공원");
                    keywords.Add("한라산");
                }
                else if (regionKeyword == "강원")
                {
                    keywords.Add("설악산국립공원");
                    keywords.Add("오대산국립공원");
                    keywords.Add("치악산국립공원");
                    keywords.Add("태백산국립공원");
                }
                else if (regionKeyword == "경상남도")
                {
                    keywords.Add("지리산국립공원");
                    keywords.Add("가야산국립공원");
                    keywords.Add("한려해상국립공원");
                    keywords.Add("덕유산국립공원");
                }
                else if (regionKeyword == "전라남도")
                {
                    keywords.Add("지리산국립공원");
                    keywords.Add("무등산국립공원");
                    keywords.Add("월출산국립공원");
                    keywords.Add("다도해해상국립공원");
                    keywords.Add("한려해상국립공원");
                }
                else
                {
                    keywords.Add("국립공원");
                }
            }
            else if (category == "문화유적")
            {
                keywords.Add(regionKeyword + " 문화재");
                keywords.Add(regionKeyword + " 유적");
                keywords.Add(regionKeyword + " 사찰");
                keywords.Add(regionKeyword + " 향교");
                keywords.Add(regionKeyword + " 서원");
                keywords.Add(regionKeyword + " 고택");
                keywords.Add(regionKeyword + " 산성");
                keywords.Add(regionKeyword + " 고분");
                keywords.Add(regionKeyword + " 현충원");
                keywords.Add(regionKeyword + " 보훈묘역");
                keywords.Add(regionKeyword + " 박물관");
            }
            else if (category == "해변")
            {
                keywords.Add(regionKeyword + " 해수욕장");
                keywords.Add(regionKeyword + " 해변");
                keywords.Add(regionKeyword + " 바다");
                keywords.Add(regionKeyword + " 해안");
                keywords.Add(regionKeyword + " 항구");
            }
            else
            {
                keywords.Add(regionKeyword + " " + category);
                keywords.Add(category);
            }

            return keywords.Distinct().ToList();
        }

        private List<TourSpotItem> MergeDistinctTours(params List<TourSpotItem>[] lists)
        {
            Dictionary<string, TourSpotItem> dict = new Dictionary<string, TourSpotItem>();

            if (lists == null)
                return new List<TourSpotItem>();

            foreach (List<TourSpotItem> list in lists)
            {
                if (list == null)
                    continue;

                foreach (TourSpotItem item in list)
                {
                    if (item == null)
                        continue;

                    string key = item.ContentId;

                    if (string.IsNullOrWhiteSpace(key))
                        key = (item.Name ?? "") + "|" + (item.Address ?? "");

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (!dict.ContainsKey(key))
                        dict.Add(key, item);
                }
            }

            return dict.Values.ToList();
        }

        private void SetTourListResult(List<TourSpotItem> list, string subText)
        {
            if (list == null)
                list = new List<TourSpotItem>();

            _tourList = list;

            TourSpotCache.SaveTours(_tourList);

            TxtResultCount.Text = _tourList.Count.ToString();
            TxtResultSub.Text = subText;

            if (_tourList.Count == 0)
            {
                ClearSelectedTour();
                MessageBox.Show("검색 결과가 없습니다.");
                return;
            }

            ApplyTourSort();

            if (_isLoggedIn)
            {
                _ = LoadFavoriteStatesAsync();
            }
        }

        private void ClearSelectedTour()
        {
            _selectedTour = null;

            TourSpotList.ItemsSource = null;
            StayList.ItemsSource = null;

            TxtDetailIcon.Text = "";
            TxtDetailName.Text = "검색 결과 없음";
            TxtDetailCategory.Text = "";
            TxtDetailAddress.Text = "";
            TxtDetailReviewSummary.Text = "☆☆☆☆☆  0.0 · 리뷰 0개";

            UpdateDetailFavoriteButton();
        }

        private string GetSelectedRegionKeyword()
        {
            ComboBoxItem item = CmbRegion.SelectedItem as ComboBoxItem;

            if (item == null)
                return "대전";

            string region = item.Tag == null ? "" : item.Tag.ToString();

            if (region.Contains("대전")) return "대전";
            if (region.Contains("서울")) return "서울";
            if (region.Contains("제주")) return "제주";
            if (region.Contains("강원")) return "강원";

            if (region.Contains("경상남도")) return "경상남도";
            if (region.Contains("경상북도")) return "경상북도";

            if (region.Contains("전라남도")) return "전라남도";
            if (region.Contains("전라북도")) return "전라북도";

            if (region.Contains("경상")) return "경상남도";
            if (region.Contains("전라")) return "전라남도";

            return region;
        }
        private string GetSelectedRegionFilterText()
        {
            ComboBoxItem item = CmbRegion.SelectedItem as ComboBoxItem;

            if (item == null)
                return "";

            string region = item.Tag == null ? "" : item.Tag.ToString();

            if (region.Contains("서울")) return "서울";
            if (region.Contains("제주")) return "제주";
            if (region.Contains("대전")) return "대전";
            if (region.Contains("강원")) return "강원";

            if (region.Contains("경상남도")) return "경상남도";
            if (region.Contains("경상북도")) return "경상북도";

            if (region.Contains("전라남도")) return "전라남도";
            if (region.Contains("전라북도")) return "전라북도";

            return region;
        }
        private bool IsMatchedSearchKeyword(TourSpotItem item, string keyword)
        {
            if (item == null)
                return false;

            if (string.IsNullOrWhiteSpace(keyword))
                return true;

            string text =
                (item.Name ?? "") + " " +
                (item.Category ?? "") + " " +
                (item.SubCategory ?? "") + " " +
                (item.Address ?? "");

            if (keyword.Contains("카페") || keyword.Contains("커피"))
            {
                return text.Contains("카페") ||
                       text.Contains("커피") ||
                       text.Contains("디저트") ||
                       text.Contains("베이커리") ||
                       text.Contains("빵");
            }

            if (keyword.Contains("맛집") || keyword.Contains("음식"))
            {
                return IsMatchedCategory(item, "맛집");
            }

            return text.Contains(keyword);
        }

        private string NormalizeCategoryName(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "전체";

            return category.Trim()
                .Replace("🌿", "")
                .Replace("📍", "")
                .Trim();
        }

        private bool IsBlockedWrongCategoryForTour(string text)
        {
            return ContainsAny(text,
                "쇼핑",
                "아울렛",
                "프리미엄아울렛",
                "백화점",
                "마트",
                "마켓",
                "상점",
                "골프",
                "골프장",
                "골프존",
                "나이키",
                "골든구스",
                "음식",
                "음식점",
                "맛집",
                "식당",
                "카페",
                "커피",
                "횟집",
                "클럽",
                "호텔",
                "모텔",
                "게스트하우스");
        }

        private bool IsMatchedCategory(TourSpotItem item, string category)
        {
            if (item == null)
                return false;

            category = NormalizeCategoryName(category);

            string name = item.Name ?? "";
            string itemCategory = item.Category ?? "";
            string subCategory = item.SubCategory ?? "";
            string address = item.Address ?? "";

            string text = name + " " + itemCategory + " " + subCategory + " " + address;

            if (category == "국립공원")
            {
                if (IsBlockedWrongCategoryForTour(text))
                    return false;

                return ContainsAny(text,
                    "국립공원",
                    "계룡산",
                    "수통골",
                    "북한산",
                    "한라산",
                    "설악산",
                    "오대산",
                    "치악산",
                    "태백산",
                    "지리산",
                    "가야산",
                    "덕유산",
                    "무등산",
                    "월출산",
                    "한려해상",
                    "다도해");
            }

            if (category == "문화유적")
            {
                if (IsBlockedWrongCategoryForTour(text))
                    return false;

                // 전시/과학/체험 시설 중 유적이 아닌 것 제거
                if (ContainsAny(text,
                    "과학관",
                    "과학교육원",
                    "아트",
                    "Art",
                    "Science",
                    "체험관",
                    "센터",
                    "수목원",
                    "식물원",
                    "동물원",
                    "아쿠아리움"))
                {
                    // 단, 현충원·박물관·유적 키워드가 있으면 문화유적으로 인정
                    if (!ContainsAny(text, "현충원", "보훈묘역", "박물관", "문화재", "유적", "역사"))
                        return false;
                }

                return ContainsAny(text,
                    "문화재",
                    "유적",
                    "사적",
                    "사찰",
                    "절",
                    "향교",
                    "서원",
                    "고택",
                    "종택",
                    "고분",
                    "왕릉",
                    "산성",
                    "읍성",
                    "성곽",
                    "궁",
                    "탑",
                    "기념관",
                    "역사",
                    "현충원",
                    "보훈묘역",
                    "박물관");
            }

            if (category == "해변")
            {
                if (IsBlockedWrongCategoryForTour(text))
                    return false;

                return ContainsAny(text,
                    "해변",
                    "해수욕장",
                    "바다",
                    "해안",
                    "항구",
                    "방파제",
                    "선착장");
            }

            if (category == "맛집")
            {
                return ContainsAny(text,
                    "음식",
                    "음식점",
                    "맛집",
                    "식당",
                    "카페",
                    "커피",
                    "빵",
                    "디저트",
                    "국수",
                    "분식",
                    "갈비",
                    "한식",
                    "중식",
                    "일식",
                    "양식",
                    "횟집");
            }

            return true;
        }

        private bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword))
                    return true;
            }

            return false;
        }

        private void SetAllCategoryButtonSelected()
        {
            foreach (var child in CategoryPanel.Children)
            {
                Button btn = child as Button;

                if (btn == null)
                    continue;

                if (btn.Content != null && btn.Content.ToString() == "전체")
                    btn.Style = (Style)FindResource("YellowButton");
                else
                    btn.Style = (Style)FindResource("GrayButton");
            }
        }

        private void SetSelectedCategoryButton(Button selectedButton)
        {
            foreach (var child in CategoryPanel.Children)
            {
                Button btn = child as Button;

                if (btn != null)
                {
                    btn.Style = (Style)FindResource("GrayButton");
                }
            }

            selectedButton.Style = (Style)FindResource("YellowButton");
        }

        private bool IsNaverLocalKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return false;

            return keyword.Contains("카페") ||
                   keyword.Contains("커피") ||
                   keyword.Contains("맛집") ||
                   keyword.Contains("음식") ||
                   keyword.Contains("식당") ||
                   keyword.Contains("빵") ||
                   keyword.Contains("디저트");
        }

        private async Task<List<TourSpotItem>> SearchNaverLocalManyAsync(params string[] keywords)
        {
            List<TourSpotItem> merged = new List<TourSpotItem>();

            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                List<TourSpotItem> list = await NaverLocalApi.SearchAsTourSpotsAsync(keyword);

                if (list != null)
                    merged.AddRange(list);
            }

            return MergeDistinctTours(merged);
        }

        #endregion

        #region 정렬

        private async void BtnSortDistance_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn != null)
                SetSelectedSortButton(btn);

            _sortMode = "거리순";

            await TryLoadMyLocationAsync();

            ApplyTourSort();
        }

        private void BtnSortRating_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn != null)
                SetSelectedSortButton(btn);

            _sortMode = "별점순";
            ApplyTourSort();
        }

        private void BtnSortReview_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn != null)
                SetSelectedSortButton(btn);

            _sortMode = "리뷰순";
            ApplyTourSort();
        }

        private void ApplyTourSort()
        {
            if (_tourList == null)
                return;

            List<TourSpotItem> sorted;

            if (_sortMode == "별점순")
            {
                sorted = _tourList
                    .OrderByDescending(x => GetRatingValue(x))
                    .ToList();
            }
            else if (_sortMode == "리뷰순")
            {
                sorted = _tourList
                    .OrderByDescending(x => GetReviewScore(x))
                    .ToList();
            }
            else if (_sortMode == "거리순")
            {
                _myMapY = WOOSONG_MAPY;
                _myMapX = WOOSONG_MAPX;
                _hasMyLocation = true;

                foreach (TourSpotItem item in _tourList)
                {
                    int meter = CalculateDistanceMeters(
                        _myMapY,
                        _myMapX,
                        item.MapY,
                        item.MapX
                    );

                    item.DistanceMeters = meter;
                    item.DistanceText = FormatDistanceText(meter);
                }

                sorted = _tourList
                    .OrderBy(x => x.DistanceMeters)
                    .ToList();

                TxtResultSub.Text = "우송대 기준 · 검색 결과";
            }
            else
            {
                sorted = _tourList.ToList();

                foreach (TourSpotItem item in sorted)
                {
                    item.DistanceMeters = 0;
                    item.DistanceText = "";
                }
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].No = i + 1;
            }

            TourSpotList.ItemsSource = null;
            TourSpotList.ItemsSource = sorted;

            if (sorted.Count > 0)
            {
                TourSpotList.SelectedItem = sorted[0];
            }
            else
            {
                ClearSelectedTour();
            }
        }

        private string FormatDistanceText(int meter)
        {
            if (meter == int.MaxValue)
                return "거리 정보 없음";

            if (meter >= 1000)
                return (meter / 1000.0).ToString("0.##") + "km";

            return meter + "m";
        }

        private double GetRatingValue(TourSpotItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Rating))
                return 0;

            string text = item.Rating.Replace("★", "").Trim();

            double value;

            if (double.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return 0;
        }

        private int GetReviewScore(TourSpotItem item)
        {
            if (item == null)
                return 0;

            string key = item.ContentId;

            if (string.IsNullOrWhiteSpace(key))
                key = item.Name;

            int hash = Math.Abs(key.GetHashCode());

            return 100 + (hash % 3000);
        }

        private void SetSelectedSortButton(Button selectedButton)
        {
            foreach (var child in SortPanel.Children)
            {
                Button btn = child as Button;

                if (btn != null)
                {
                    btn.Style = (Style)FindResource("GrayButton");
                }
            }

            selectedButton.Style = (Style)FindResource("YellowButton");
        }

        #endregion

        #region 지도

        private async void LoadKakaoMap()
        {
            try
            {
                await MapWebView.EnsureCoreWebView2Async();

                _isMapLoaded = false;

                MapWebView.CoreWebView2.WebMessageReceived += async (s, e) =>
                {
                    string message = e.TryGetWebMessageAsString();

                    if (message == "MAP_READY")
                    {
                        _isMapLoaded = true;

                        if (_selectedTour != null)
                        {
                            await MoveMapToTourAsync(_selectedTour);
                            await ShowTourImageOnMapAsync(_selectedTour);
                        }

                        return;
                    }

                    if (message != null && message.StartsWith("OPEN_LINK|"))
                    {
                        string url = message.Substring("OPEN_LINK|".Length);

                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }

                        return;
                    }
                };

                string kakaoKey = "54dc707b933a254382b930502bb97ddf";

                string html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        html, body, #map {{
            width: 100%;
            height: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            font-family: 'Malgun Gothic', sans-serif;
        }}

        #tourCard {{
            position: absolute;
            left: 28px;
            bottom: 24px;
            width: 350px;
            min-height: 88px;
            background: white;
            border-radius: 18px;
            border: 1px solid #eeeeee;
            box-shadow: 0 10px 28px rgba(0,0,0,0.22);
            overflow: hidden;
            z-index: 9999;
            display: none;
        }}

        #tourCardImageWrap {{
            position: relative;
            width: 100%;
            height: 162px;
            background: #f4f4f4;
            overflow: hidden;
        }}

        #tourCardImage {{
            width: 100%;
            height: 162px;
            object-fit: cover;
            display: block;
        }}

        #tourCardNoImage {{
            width: 100%;
            height: 162px;
            display: none;
            align-items: center;
            justify-content: center;
            font-size: 48px;
            opacity: 0.5;
            background: linear-gradient(135deg, #FFFBEA, #E8F4FF);
        }}

        #tourCardCategory {{
            position: absolute;
            left: 10px;
            top: 10px;
            background: rgba(255,255,255,0.88);
            border-radius: 10px;
            padding: 4px 9px;
            font-size: 11px;
            font-weight: bold;
            color: #333333;
            max-width: 120px;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}

        #tourCardBody {{
            padding: 10px 14px;
            box-sizing: border-box;
        }}

        #tourCardTitle {{
            font-size: 15px;
            font-weight: 800;
            color: #222222;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}

        #tourCardAddress {{
            margin-top: 5px;
            font-size: 12px;
            color: #999999;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}

        #tourCardLink {{
            display: none;
            margin-top: 8px;
            padding: 5px 10px;
            border-radius: 999px;
            background: #f2f6ff;
            color: #246BFE;
            font-size: 12px;
            font-weight: 800;
            text-decoration: none;
            cursor: pointer;
        }}

        #tourCardLink:hover {{
            background: #e6efff;
        }}
    </style>

    <script src='https://dapi.kakao.com/v2/maps/sdk.js?appkey={kakaoKey}&autoload=false'></script>
</head>
<body>
    <div id='map'></div>

    <div id='tourCard'>
        <div id='tourCardImageWrap'>
            <img id='tourCardImage' src='' />
            <div id='tourCardNoImage'>🏞️</div>
            <div id='tourCardCategory'>관광지</div>
        </div>
        <div id='tourCardBody'>
            <div id='tourCardTitle'>관광지명</div>
            <div id='tourCardAddress'>주소</div>
            <a id='tourCardLink' href='#'>상세 정보</a>
        </div>
    </div>

    <script>
        var map;
        var marker;
        var infowindow;

        kakao.maps.load(function() {{
            var container = document.getElementById('map');

            var options = {{
                center: new kakao.maps.LatLng(37.5665, 126.9780),
                level: 7
            }};

            map = new kakao.maps.Map(container, options);

            marker = new kakao.maps.Marker({{
                position: new kakao.maps.LatLng(37.5665, 126.9780),
                map: map
            }});

            infowindow = new kakao.maps.InfoWindow({{
                content: '<div style=""padding:6px 10px;font-size:12px;font-weight:bold;"">서울특별시</div>'
            }});

            infowindow.open(map, marker);

            window.moveMap = function(lat, lng, title) {{
                var position = new kakao.maps.LatLng(lat, lng);

                map.setCenter(position);
                map.setLevel(6);

                marker.setPosition(position);
                marker.setMap(map);

                infowindow.setContent(
                    '<div style=""padding:6px 10px;font-size:12px;font-weight:bold;"">' 
                    + title + 
                    '</div>'
                );

                infowindow.open(map, marker);
            }};

            window.showTourCard = function(title, address, category, imageUrl, linkUrl) {{
                var card = document.getElementById('tourCard');
                var imgWrap = document.getElementById('tourCardImageWrap');
                var img = document.getElementById('tourCardImage');
                var noImg = document.getElementById('tourCardNoImage');
                var link = document.getElementById('tourCardLink');

                document.getElementById('tourCardTitle').textContent = title || '관광지명 없음';
                document.getElementById('tourCardAddress').textContent = address || '주소 정보 없음';
                document.getElementById('tourCardCategory').textContent = category || '관광지';

                card.style.display = 'block';

                // linkUrl이 있으면 네이버 검색 결과라고 보고 이미지 영역을 아예 숨김
                if (linkUrl && linkUrl.trim().length > 0) {{
                    img.removeAttribute('src');
                    img.style.display = 'none';
                    noImg.style.display = 'none';
                    imgWrap.style.display = 'none';

                    card.style.height = 'auto';

                    link.style.display = 'inline-block';
                    link.href = linkUrl;

                    link.onclick = function() {{
                        if (window.chrome && window.chrome.webview) {{
                            window.chrome.webview.postMessage('OPEN_LINK|' + linkUrl);
                        }}
                        else {{
                            window.open(linkUrl, '_blank');
                        }}

                        return false;
                    }};

                    return;
                }}

                // 링크가 없으면 관광공사 결과라서 기존처럼 이미지 영역 유지
                imgWrap.style.display = 'block';
                card.style.height = '230px';

                link.style.display = 'none';
                link.removeAttribute('href');
                link.onclick = null;

                if (imageUrl && imageUrl.trim().length > 0) {{
                    noImg.style.display = 'none';
                    img.style.display = 'block';

                    img.onerror = function() {{
                        img.style.display = 'none';
                        noImg.style.display = 'flex';
                    }};

                    img.src = imageUrl;
                }}
                else {{
                    img.removeAttribute('src');
                    img.style.display = 'none';
                    noImg.style.display = 'flex';
                }}
            }};

            if (window.chrome && window.chrome.webview) {{
                window.chrome.webview.postMessage('MAP_READY');
            }}
        }});
    </script>
</body>
</html>";

                MapWebView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "카카오 지도 오류");
            }
        }
        private async Task MoveMapToTourAsync(TourSpotItem item)
        {
            if (item == null)
                return;

            if (!_isMapLoaded)
                return;

            if (MapWebView.CoreWebView2 == null)
                return;

            if (item.MapX == 0 || item.MapY == 0)
                return;

            try
            {
                string title = EscapeJavaScriptString(item.Name);

                string script = string.Format(
                    CultureInfo.InvariantCulture,
                    "window.moveMap({0}, {1}, '{2}');",
                    item.MapY,
                    item.MapX,
                    title
                );

                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "지도 이동 오류");
            }
        }

        private async Task ShowTourImageOnMapAsync(TourSpotItem item)
        {
            if (item == null)
                return;

            if (!_isMapLoaded)
                return;

            if (MapWebView.CoreWebView2 == null)
                return;

            try
            {
                string title = EscapeJavaScriptString(item.Name);
                string address = EscapeJavaScriptString(item.Address);
                string category = EscapeJavaScriptString(item.Category);
                string imageUrl = EscapeJavaScriptString(item.ImageUrl);

                string linkUrl = "";

                // 네이버 지역검색 결과만 링크 표시
                // 관광공사 관광지는 링크 버튼 안 뜸
                if (item.ContentTypeId == "NAVER" && !string.IsNullOrWhiteSpace(item.Link))
                {
                    linkUrl = EscapeJavaScriptString(item.Link);
                }

                string script = string.Format(
                    CultureInfo.InvariantCulture,
                    "window.showTourCard('{0}', '{1}', '{2}', '{3}', '{4}');",
                    title,
                    address,
                    category,
                    imageUrl,
                    linkUrl
                );

                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
            }
        }

        private string EscapeJavaScriptString(string text)
        {
            if (text == null)
                return "";

            return text
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        #endregion

        #region 숙소

        private async Task LoadNearbyStayPreviewAsync(TourSpotItem tour)
        {
            if (tour == null)
                return;

            if (tour.MapX == 0 || tour.MapY == 0)
            {
                _nearbyStayList = new List<StayItem>();
                StayList.ItemsSource = null;
                StayList.ItemsSource = _nearbyStayList;
                return;
            }

            try
            {
                StayList.ItemsSource = null;

                // 429가 걸린 동안 자동 미리보기 API는 다시 호출하지 않음
                // 단, 가짜 숙소 목록은 만들지 않고 그냥 비워둠
                if (DateTime.Now < _accommodationBlockedUntil)
                {
                    _nearbyStayList = new List<StayItem>();
                    StayList.ItemsSource = _nearbyStayList;
                    return;
                }

                List<AccommodationResponse> accoms =
                    await AccommodationApi.GetNearbyAsync(tour.MapY, tour.MapX, 7);

                if (accoms == null || accoms.Count == 0)
                {
                    _nearbyStayList = new List<StayItem>();
                    StayList.ItemsSource = _nearbyStayList;
                    return;
                }

                _nearbyStayList = accoms
                    .Select((a, index) => ConvertToStayItem(a, index + 1))
                    .ToList();

                StayList.ItemsSource = null;
                StayList.ItemsSource = _nearbyStayList;
            }
            catch (Exception ex)
            {
                if (IsTooManyRequests(ex))
                {
                    // 팝업 지옥 방지: 자동 미리보기에서는 429를 조용히 무시
                    _accommodationBlockedUntil = DateTime.Now.AddMinutes(10);
                    _nearbyStayList = new List<StayItem>();
                    StayList.ItemsSource = null;
                    StayList.ItemsSource = _nearbyStayList;
                    return;
                }

                MessageBox.Show(ex.ToString(), "관광공사 숙소 API 오류");
            }
        }

        private async void BtnNearbyStay_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTour == null)
            {
                MessageBox.Show("관광지를 먼저 선택하세요.");
                return;
            }

            try
            {
                Mouse.OverrideCursor = null;

                List<AccommodationResponse> accoms =
                    await AccommodationApi.GetNearbyAsync(_selectedTour.MapY, _selectedTour.MapX, 50);

                if (accoms == null || accoms.Count == 0)
                {
                    MessageBox.Show("주변 숙소 검색 결과가 없습니다.");
                    return;
                }

                _nearbyStayList = accoms
                    .Select((a, index) => ConvertToStayItem(a, index + 1))
                    .ToList();

                StayListWindow window = new StayListWindow(_selectedTour, _nearbyStayList);
                window.Owner = this;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                string message = ex.ToString();

                if (message.Contains("429") || message.Contains("Too Many Requests"))
                {
                    MessageBox.Show(
                        "숙소 API 호출 제한이 걸려 있습니다.\n10~30분 뒤에 다시 눌러보세요.\n\n지금은 관광지/지도/리뷰/즐겨찾기 기능 먼저 확인하면 됩니다.",
                        "숙소 API 호출 제한"
                    );
                    return;
                }

                MessageBox.Show(ex.ToString(), "주변 숙소 전체 조회 오류");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        private bool IsTooManyRequests(Exception ex)
        {
            if (ex == null)
                return false;

            string message = ex.ToString();

            return message.Contains("429") ||
                   message.Contains("Too Many Requests");

        }

        private void BtnBookStay_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement fe = sender as FrameworkElement;

            if (fe == null)
                return;

            StayItem stay = fe.DataContext as StayItem;

            if (stay == null)
            {
                MessageBox.Show("숙소 정보를 찾을 수 없습니다.");
                return;
            }

            string url = stay.BookingUrl;

            if (string.IsNullOrWhiteSpace(url))
            {
                url = "https://search.naver.com/search.naver?query="
                      + Uri.EscapeDataString(stay.Name + " 예약");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private StayItem ConvertToStayItem(AccommodationResponse a, int no)
        {
            return new StayItem
            {
                No = no,
                Name = a.Name,
                Address = a.Address,
                HotelType = string.IsNullOrWhiteSpace(a.AccomType) ? "숙소" : a.AccomType,
                Icon = "🏨",

                Distance = "🚗 " + a.DistanceKm.ToString("0.##") + "km",
                Price = MakeStayPriceText(a),
                Rating = "★ 4.0",
                ReviewText = "리뷰 정보 없음",

                BookingUrl = a.BookingUrl,

                MapX = a.Longitude,
                MapY = a.Latitude,

                Tag1 = "숙소",
                Tag2 = string.IsNullOrWhiteSpace(a.AccomType) ? "추천" : a.AccomType,
                Tag3 = "주변 추천"
            };
        }

        private string MakeStayPriceText(AccommodationResponse a)
        {
            if (a == null)
                return "가격 정보 없음";

            string name = a.Name ?? "";
            string type = a.AccomType ?? "";

            int basePrice = 65000;

            string text = name + " " + type;

            if (text.Contains("게스트") || text.ToLower().Contains("guest"))
                basePrice = 35000;
            else if (text.Contains("모텔"))
                basePrice = 50000;
            else if (text.Contains("펜션"))
                basePrice = 75000;
            else if (text.Contains("호텔") || text.ToLower().Contains("hotel"))
                basePrice = 95000;
            else if (text.Contains("리조트"))
                basePrice = 120000;

            int seed = 0;

            for (int i = 0; i < name.Length; i++)
            {
                seed += name[i];
            }

            int extra = (seed % 5) * 10000;

            int price = basePrice + extra;

            return price.ToString("N0") + "원~";
        }

        #endregion

        #region 로그인 / 마이페이지

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            OpenLoginWindow();
        }

        public async void SetLoginUser(string userId)
        {
            _isLoggedIn = true;
            _loginUserId = userId;

            _loginMemberId = 1;

            BtnLogin.Visibility = Visibility.Collapsed;

            TxtLoginUser.Text = "👤 " + _loginUserId;
            UserBox.Visibility = Visibility.Visible;

            await LoadFavoriteStatesAsync();
        }

        private void UserBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MyPageWindow window = new MyPageWindow(_loginUserId);
            window.Owner = this;
            window.ShowDialog();
        }

        private void UserBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_loginUserId))
            {
                MessageBox.Show("로그인 정보가 없습니다.");
                return;
            }

            MyPageWindow window = new MyPageWindow(_loginUserId);
            window.Owner = this;
            window.ShowDialog();
        }

        #endregion

        #region 즐겨찾기

        private async void BtnTourCardFavorite_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            Button btn = sender as Button;

            if (btn == null)
                return;

            TourSpotItem item = btn.DataContext as TourSpotItem;

            if (item == null)
            {
                MessageBox.Show("관광지 정보를 찾을 수 없습니다.");
                return;
            }

            _selectedTour = item;
            TourSpotList.SelectedItem = item;

            await ToggleFavoriteTourAsync(item);
        }

        private async void BtnDetailFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTour == null)
            {
                MessageBox.Show("관광지를 먼저 선택하세요.");
                return;
            }

            await ToggleFavoriteTourAsync(_selectedTour);
        }

        private async Task ToggleFavoriteTourAsync(TourSpotItem tour)
        {
            if (tour == null)
                return;

            if (string.IsNullOrWhiteSpace(_loginUserId))
            {
                MessageBox.Show("로그인 후 즐겨찾기를 사용할 수 있습니다.");
                return;
            }

            if (tour.IsFavorite)
            {
                await RemoveFavoriteTourAsync(tour);
            }
            else
            {
                await AddFavoriteTourAsync(tour);
            }
        }

        private async Task RemoveFavoriteTourAsync(TourSpotItem tour)
        {
            if (tour == null)
                return;

            int targetId;

            if (!int.TryParse(tour.ContentId, out targetId))
            {
                MessageBox.Show("관광지 ID가 올바르지 않습니다.");
                return;
            }

            SetFavoriteState(tour, false);

            try
            {
                List<FavoriteResponse> favorites =
                    await FavoriteApi.GetFavoritesByMemberAsync(_loginMemberId);

                if (favorites == null)
                    favorites = new List<FavoriteResponse>();

                FavoriteResponse targetFavorite = favorites.FirstOrDefault(x =>
                    x.TargetId == targetId &&
                    IsSameTargetType(x.TargetType, "TouristSpot")
                );

                if (targetFavorite == null)
                {
                    SetFavoriteState(tour, false);
                    return;
                }

                bool ok = await FavoriteApi.DeleteFavoriteAsync(targetFavorite.FavoriteId);

                if (!ok)
                {
                    SetFavoriteState(tour, true);
                }
            }
            catch (Exception ex)
            {
                SetFavoriteState(tour, true);
                MessageBox.Show(ex.ToString(), "즐겨찾기 삭제 오류");
            }
        }

        private async Task AddFavoriteTourAsync(TourSpotItem tour)
        {
            if (tour == null)
                return;

            if (string.IsNullOrWhiteSpace(_loginUserId))
            {
                MessageBox.Show("로그인 후 즐겨찾기를 등록할 수 있습니다.");
                return;
            }

            int targetId;

            if (!int.TryParse(tour.ContentId, out targetId))
            {
                MessageBox.Show("관광지 ID가 올바르지 않습니다.");
                return;
            }

            FavoriteNameStore.SaveName("TouristSpot", targetId, tour.Name);

            SetFavoriteState(tour, true);

            try
            {
                FavoriteRequest request = new FavoriteRequest
                {
                    MemberId = _loginMemberId,
                    TargetType = "TouristSpot",
                    TargetId = targetId
                };

                bool ok = await FavoriteApi.AddFavoriteAsync(request);

                if (!ok)
                {
                    SetFavoriteState(tour, false);
                }
            }
            catch (Exception ex)
            {
                SetFavoriteState(tour, false);
                MessageBox.Show(ex.ToString(), "즐겨찾기 등록 오류");
            }
        }

        private void SetFavoriteState(TourSpotItem tour, bool isFavorite)
        {
            if (tour == null)
                return;

            tour.IsFavorite = isFavorite;

            if (_tourList != null)
            {
                foreach (TourSpotItem item in _tourList)
                {
                    if (item.ContentId == tour.ContentId)
                    {
                        item.IsFavorite = isFavorite;
                    }
                }
            }

            TourSpotList.Items.Refresh();
            UpdateDetailFavoriteButton();
        }

        private bool IsSameTargetType(string value, string targetType)
        {
            string a = NormalizeTargetType(value);
            string b = NormalizeTargetType(targetType);

            return a == b;
        }

        private string NormalizeTargetType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            if (value == "TouristSpot") return "TouristSpot";
            if (value == "SPOT") return "TouristSpot";
            if (value == "관광지") return "TouristSpot";

            if (value == "Accommodation") return "Accommodation";
            if (value == "ACCOM") return "Accommodation";
            if (value == "숙소") return "Accommodation";

            return value;
        }

        private void UpdateDetailFavoriteButton()
        {
            if (BtnDetailFavorite == null)
                return;

            if (_selectedTour != null && _selectedTour.IsFavorite)
            {
                BtnDetailFavorite.Content = "♥ 즐겨찾기";
                BtnDetailFavorite.Foreground = new SolidColorBrush(Color.FromRgb(255, 75, 92));
            }
            else
            {
                BtnDetailFavorite.Content = "♡ 즐겨찾기";
                BtnDetailFavorite.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            }
        }

        private async Task LoadFavoriteStatesAsync()
        {
            if (!_isLoggedIn)
                return;

            if (_tourList == null || _tourList.Count == 0)
                return;

            try
            {
                List<FavoriteResponse> favorites =
                    await FavoriteApi.GetFavoritesByMemberAsync(_loginMemberId);

                if (favorites == null)
                    favorites = new List<FavoriteResponse>();

                HashSet<int> favoriteTargetIds = new HashSet<int>(
                    favorites
                        .Where(x => NormalizeTargetType(x.TargetType) == "TouristSpot")
                        .Select(x => x.TargetId)
                );

                foreach (TourSpotItem tour in _tourList)
                {
                    int targetId;

                    if (int.TryParse(tour.ContentId, out targetId))
                    {
                        tour.IsFavorite = favoriteTargetIds.Contains(targetId);
                    }
                    else
                    {
                        tour.IsFavorite = false;
                    }
                }

                TourSpotList.Items.Refresh();
                UpdateDetailFavoriteButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "즐겨찾기 상태 조회 오류");
            }
        }

        #endregion

        #region 리뷰

        private async void BtnWriteReview_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTour == null)
            {
                MessageBox.Show("관광지를 먼저 선택하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_loginUserId))
            {
                MessageBox.Show("로그인 후 리뷰를 작성할 수 있습니다.");
                return;
            }

            ReviewWriteWindow window = new ReviewWriteWindow(_selectedTour, _loginUserId);
            window.Owner = this;

            bool? result = window.ShowDialog();

            if (result == true)
            {
                MyPageWindow myPage = new MyPageWindow(_loginUserId);
                myPage.Owner = this;
                myPage.ShowDialog();

                await LoadReviewSummaryAsync(_selectedTour);
            }
        }

        private async Task LoadReviewSummaryAsync(TourSpotItem tour)
        {
            if (tour == null)
            {
                TxtDetailReviewSummary.Text = "☆☆☆☆☆  0.0 · 리뷰 0개";
                return;
            }

            int targetId;

            if (!int.TryParse(tour.ContentId, out targetId))
            {
                TxtDetailReviewSummary.Text = "☆☆☆☆☆  0.0 · 리뷰 0개";
                return;
            }

            try
            {
                List<ReviewResponse> reviews =
                    await ReviewApi.GetReviewsByTargetAsync("TouristSpot", targetId);

                if (reviews == null || reviews.Count == 0)
                {
                    TxtDetailReviewSummary.Text = "☆☆☆☆☆  0.0 · 리뷰 0개";
                    return;
                }

                double avg = reviews.Average(x => x.Rating);
                int count = reviews.Count;

                int starCount = (int)Math.Round(avg);

                if (starCount < 0)
                    starCount = 0;

                if (starCount > 5)
                    starCount = 5;

                string stars = new string('★', starCount) + new string('☆', 5 - starCount);

                TxtDetailReviewSummary.Text =
                    stars + "  " + avg.ToString("0.0") + " · 리뷰 " + count + "개";
            }
            catch
            {
                TxtDetailReviewSummary.Text = "☆☆☆☆☆  0.0 · 리뷰 0개";
            }
        }

        #endregion

        #region 윈도우 상단바

        private void BtnWindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState.Minimized;
        }

        private void BtnWindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void BtnWindowClose_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button)
                return;

            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                else
                    WindowState = WindowState.Maximized;

                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
            }
        }

        #endregion

        #region 거리 계산

        private int CalculateDistanceMeters(double lat1, double lng1, double lat2, double lng2)
        {
            if (lat1 == 0 || lng1 == 0 || lat2 == 0 || lng2 == 0)
                return int.MaxValue;

            const double earthRadius = 6371000;

            double dLat = ToRadian(lat2 - lat1);
            double dLng = ToRadian(lng2 - lng1);

            double rLat1 = ToRadian(lat1);
            double rLat2 = ToRadian(lat2);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return (int)Math.Round(earthRadius * c);
        }

        private double ToRadian(double degree)
        {
            return degree * Math.PI / 180.0;
        }

        private async Task<bool> TryLoadMyLocationAsync()
        {
            SetMyLocationToWoosong();

            await Task.CompletedTask;
            return true;
        }

        #endregion

        #region 로그아웃 / 로그인창

        public void LogoutAndShowLogin()
        {
            _isLoggedIn = false;
            _loginUserId = "";

            BtnLogin.Visibility = Visibility.Visible;

            TxtLoginUser.Text = "";
            UserBox.Visibility = Visibility.Collapsed;

            object logoutObj = FindName("BtnLogout");

            if (logoutObj is Button logoutButton)
            {
                logoutButton.Visibility = Visibility.Collapsed;
            }

            OpenLoginWindow();
        }

        private void OpenLoginWindow()
        {
            try
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Owner = this;

                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    SetLoginUser(loginWindow.LoginUserId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "로그인창 오류");
            }
        }

        #endregion

        #region 이미지 오버레이

        private void UpdateTourImageOverlay(TourSpotItem item)
        {
            if (item == null)
            {
                TourImageOverlay.Visibility = Visibility.Collapsed;
                ImgTourPreview.Source = null;
                return;
            }

            TxtTourImageTitle.Text = item.Name;
            TxtTourImageAddress.Text = item.Address;
            TxtTourImageCategory.Text = item.Category;

            TourImageOverlay.Visibility = Visibility.Visible;

            if (string.IsNullOrWhiteSpace(item.ImageUrl))
            {
                ImgTourPreview.Source = null;
                TxtTourImageAddress.Text = item.Address + " · 이미지 정보 없음";
                return;
            }

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(item.ImageUrl, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();

                ImgTourPreview.Source = bitmap;
            }
            catch
            {
                ImgTourPreview.Source = null;
                TxtTourImageAddress.Text = item.Address + " · 이미지 로드 실패";
            }
        }

        #endregion

        #region 공유 / 친구창

        private void BtnTourCardShare_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            FrameworkElement fe = sender as FrameworkElement;

            if (fe == null)
                return;

            TourSpotItem item = fe.DataContext as TourSpotItem;

            if (item == null)
                return;

            _selectedTour = item;
            TourSpotList.SelectedItem = item;

            OpenFriendWindow(item);
        }

        private void BtnShareTour_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTour == null)
            {
                MessageBox.Show("관광지를 먼저 선택하세요.");
                return;
            }

            OpenFriendWindow(_selectedTour);
        }

        private void FriendBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            OpenFriendWindow(null);
        }

        private void PositionFriendWindow(Window window)
        {
            if (window == null)
                return;

            double chatWidth = 420;
            double rightMargin = 8;

            if (MapWebView == null || MapWebView.ActualWidth <= 0 || MapWebView.ActualHeight <= 0)
            {
                window.Width = chatWidth;
                window.Height = 560;

                Point fallbackPoint = PointToScreen(new Point(ActualWidth - chatWidth - rightMargin, 130));

                PresentationSource fallbackSource = PresentationSource.FromVisual(this);

                if (fallbackSource != null && fallbackSource.CompositionTarget != null)
                {
                    fallbackPoint = fallbackSource.CompositionTarget.TransformFromDevice.Transform(fallbackPoint);
                }

                window.Left = fallbackPoint.X;
                window.Top = fallbackPoint.Y;
                return;
            }

            double xInMap = MapWebView.ActualWidth - chatWidth - rightMargin;
            double yInMap = 0;

            if (xInMap < 0)
                xInMap = 0;

            Point screenPoint = MapWebView.PointToScreen(new Point(xInMap, yInMap));

            PresentationSource source = PresentationSource.FromVisual(this);

            if (source != null && source.CompositionTarget != null)
            {
                screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
            }

            window.Width = chatWidth;
            window.Height = MapWebView.ActualHeight;

            window.Left = screenPoint.X;
            window.Top = screenPoint.Y;
        }

        private void OpenFriendWindow(TourSpotItem sharedTour)
        {
            try
            {
                if (_friendWindow == null || !_friendWindow.IsVisible)
                {
                    _friendWindow = new FriendWindow(_loginUserId, _loginMemberId);
                    _friendWindow.Owner = this;
                    _friendWindow.WindowStartupLocation = WindowStartupLocation.Manual;

                    PositionFriendWindow(_friendWindow);

                    _friendWindow.Closed += (s, e) =>
                    {
                        _friendWindow = null;
                    };

                    _friendWindow.Show();
                }
                else
                {
                    PositionFriendWindow(_friendWindow);
                    _friendWindow.Activate();
                }

                if (sharedTour != null)
                {
                    _friendWindow.ShareTourFromMain(sharedTour);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "친구창 열기 오류");
            }
        }

        public async void ShowSharedPlaceOnMap(string name, string address, double mapX, double mapY)
        {
            try
            {
                if (mapX == 0 || mapY == 0)
                {
                    string query = name + " " + address;
                    string url = "https://map.kakao.com/link/search/" + Uri.EscapeDataString(query);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });

                    return;
                }

                if (!_isMapLoaded)
                {
                    MessageBox.Show("지도가 아직 로딩 중입니다. 잠깐 뒤에 다시 눌러주세요.");
                    return;
                }

                if (MapWebView.CoreWebView2 == null)
                    return;

                string title = EscapeJavaScriptString(name);

                string script = string.Format(
                    CultureInfo.InvariantCulture,
                    "window.moveMap({0}, {1}, '{2}');",
                    mapY,
                    mapX,
                    title
                );

                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
                Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "채팅 지도 이동 오류");
            }
        }

        #endregion
    }
}