using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace 코빚노_프로젝트
{
    public partial class MyPageWindow : Window
    {
        private readonly string _loginUserId;


        public MyPageWindow(string loginUserId)
        {
            InitializeComponent();

            _loginUserId = loginUserId;

            Loaded += MyPageWindow_Loaded;
        }

        private async void MyPageWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtUserId.Text = "@" + _loginUserId;

            await LoadMyFavoritesAsync();
            await LoadMyReviewsAsync();
        }

        private async Task LoadMyReviewsAsync()
        {
            try
            {
                int memberId = 1; // 테스트용

                List<ReviewResponse> reviews =
                    await ReviewApi.GetReviewsByMemberAsync(memberId);

                if (reviews == null)
                    reviews = new List<ReviewResponse>();

                foreach (ReviewResponse review in reviews)
                {
                    if (string.IsNullOrWhiteSpace(review.TargetName))
                    {
                        string targetTypeKey = ConvertTargetTypeForStore(review.TargetType);

                        string savedName =
                            FavoriteNameStore.GetName(targetTypeKey, review.TargetId);

                        if (!string.IsNullOrWhiteSpace(savedName))
                        {
                            review.TargetName = savedName;
                        }
                        else
                        {
                            review.TargetName = "장소명 없음";
                        }
                    }

                    // 화면에는 예쁘게 표시
                    if (review.TargetType == "TouristSpot")
                    {
                        review.TargetType = "관광지";
                    }
                    else if (review.TargetType == "Accommodation")
                    {
                        review.TargetType = "숙소";
                    }
                }

                MyReviewList.ItemsSource = reviews;

                TxtReviewEmpty.Visibility =
                    reviews.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                UpdateReviewCount(reviews.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "내 리뷰 조회 오류");
                UpdateReviewCount(0);
            }
        }

        private string ConvertTargetTypeForStore(string targetType)
        {
            if (string.IsNullOrWhiteSpace(targetType))
                return "TouristSpot";

            if (targetType == "TouristSpot")
                return "TouristSpot";

            if (targetType == "관광지")
                return "TouristSpot";

            if (targetType == "SPOT")
                return "TouristSpot";

            if (targetType == "Accommodation")
                return "Accommodation";

            if (targetType == "숙소")
                return "Accommodation";

            if (targetType == "ACCOM")
                return "Accommodation";

            return targetType;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = Owner as MainWindow;

            Close();

            if (main != null)
            {
                main.Dispatcher.BeginInvoke(new Action(() =>
                {
                    main.LogoutAndShowLogin();
                }));
            }
        }



        private async Task LoadMyFavoritesAsync()
        {
            try
            {
                int memberId = 1;

                List<FavoriteResponse> favorites =
                    await FavoriteApi.GetFavoritesByMemberAsync(memberId);

                foreach (FavoriteResponse favorite in favorites)
                {
                    if (string.IsNullOrWhiteSpace(favorite.TargetName))
                    {
                        string savedName =
                            FavoriteNameStore.GetName(favorite.TargetType, favorite.TargetId);

                        if (!string.IsNullOrWhiteSpace(savedName))
                        {
                            favorite.TargetName = savedName;
                        }
                        else
                        {
                            favorite.TargetName = "장소명 없음";
                        }
                    }
                }

                MyFavoriteList.ItemsSource = favorites;

                TxtFavoriteEmpty.Visibility =
                    favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                UpdateFavoriteCount(favorites.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "즐겨찾기 조회 오류");
                UpdateFavoriteCount(0);
            }
        }
        private void UpdateFavoriteCount(int count)
        {
            TxtTopFavoriteCount.Text = count.ToString();
            TxtSideFavoriteCount.Text = count.ToString();
            BtnMenuFavorite.Content = "♡  즐겨찾기       " + count;
        }

        private void UpdateReviewCount(int count)
        {
            TxtTopReviewCount.Text = count.ToString();
            TxtSideReviewCount.Text = count.ToString();
            BtnMenuReview.Content = "★  내 리뷰       " + count;
        }

        private void BtnWindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState.Minimized;
        }

        private void BtnWindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
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
        private string ConvertTargetTypeText(string targetType)
        {
            if (string.IsNullOrWhiteSpace(targetType))
                return "";

            if (targetType == "TouristSpot")
                return "관광지";

            if (targetType == "Accommodation")
                return "숙소";

            if (targetType == "관광지")
                return "관광지";

            if (targetType == "숙소")
                return "숙소";

            return targetType;
        }
    }
}