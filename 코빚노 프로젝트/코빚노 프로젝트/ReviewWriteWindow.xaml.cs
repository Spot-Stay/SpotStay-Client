using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace 코빚노_프로젝트
{
    public partial class ReviewWriteWindow : Window
    {
        private readonly TourSpotItem _tour;
        private readonly string _loginUserId;

        private int _selectedRating = 0;

        public ReviewWriteWindow(TourSpotItem tour, string loginUserId)
        {
            InitializeComponent();

            _tour = tour;
            _loginUserId = loginUserId;

            InitWindow();
        }

        private void InitWindow()
        {
            if (_tour == null)
                return;

            // 맨 위에 내가 고른 관광지 이름 뜨게 함
            TxtPlaceName.Text = _tour.Name;
            TxtPlaceSub.Text = "리뷰를 작성해 주세요";

            TxtReviewContent.Text = "";
            TxtCharCount.Text = "0 / 500자";

            SetRating(0);
        }

        private void BtnStar_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == null)
                return;

            int rating;

            if (!int.TryParse(btn.Tag.ToString(), out rating))
                return;

            SetRating(rating);
        }

        private void SetRating(int rating)
        {
            _selectedRating = rating;

            Button[] stars =
            {
                BtnStar1,
                BtnStar2,
                BtnStar3,
                BtnStar4,
                BtnStar5
            };

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].Content = "★";

                if (i < rating)
                {
                    stars[i].Foreground =
                        new SolidColorBrush(Color.FromRgb(255, 204, 0));
                }
                else
                {
                    stars[i].Foreground =
                        new SolidColorBrush(Color.FromRgb(217, 217, 217));
                }
            }

            if (rating == 0)
            {
                TxtRatingText.Text = "선택 안 함";
            }
            else
            {
                TxtRatingText.Text = rating + "점";
            }
        }

        private void TxtReviewContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtReviewContent == null || TxtCharCount == null)
                return;

            string text = TxtReviewContent.Text;

            if (text.Length > 500)
            {
                TxtReviewContent.Text = text.Substring(0, 500);
                TxtReviewContent.SelectionStart = TxtReviewContent.Text.Length;
                return;
            }

            TxtCharCount.Text = text.Length + " / 500자";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_tour == null)
            {
                MessageBox.Show("관광지 정보가 없습니다.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_loginUserId))
            {
                MessageBox.Show("로그인 후 리뷰를 작성할 수 있습니다.");
                return;
            }

            if (_selectedRating <= 0)
            {
                MessageBox.Show("별점을 선택해 주세요.");
                return;
            }

            string content = TxtReviewContent.Text.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("리뷰 내용을 입력해 주세요.");
                return;
            }

            int targetId;

            if (!int.TryParse(_tour.ContentId, out targetId))
            {
                MessageBox.Show("관광지 ID가 올바르지 않습니다.");
                return;
            }

            try
            {
                // 마이페이지에서 장소명 없음 방지
                FavoriteNameStore.SaveName("TouristSpot", targetId, _tour.Name);

                ReviewRequest request = new ReviewRequest
                {
                    MemberId = 1,
                    TargetType = "TouristSpot",
                    TargetId = targetId,
                    Rating = _selectedRating,
                    Content = content
                };

                bool ok = await ReviewApi.AddReviewAsync(request);

                if (ok)
                {
                    // MainWindow에서 result == true 보고 마이페이지 열게 함
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("리뷰 등록에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "리뷰 등록 오류");
            }
        }
    }
}