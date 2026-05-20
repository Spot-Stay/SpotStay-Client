using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace 코빚노_프로젝트
{
    public partial class LoginWindow : Window
    {
        public string LoginUserId { get; set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void BtnDoLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string id = TxtId.Text;
                string pw = TxtPw.Password;

                bool result = await AuthApi.LoginAsync(id, pw);

                if (result)
                {
                    LoginUserId = id;

                    DialogResult = true;

                    Close();
                }
                else
                {
                    MessageBox.Show("아이디 또는 비밀번호가 틀렸습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "로그인 오류");
            }
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

            try
            {
                DialogResult = false;
            }
            catch
            {
                Close();
            }
        }

        private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                }

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
    }
}