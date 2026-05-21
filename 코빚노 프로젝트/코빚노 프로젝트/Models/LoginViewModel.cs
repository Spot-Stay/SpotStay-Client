using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace 코빚노_프로젝트
{
    public class LoginViewModel : ViewModelBase
    {
        private string userId;
        private string password;
        private bool isRememberLogin;
        private bool isLoading;
        private string errorMessage;

        public string UserId
        {
            get { return userId; }
            set { SetProperty(ref userId, value); }
        }

        public string Password
        {
            get { return password; }
            set { SetProperty(ref password, value); }
        }

        public bool IsRememberLogin
        {
            get { return isRememberLogin; }
            set { SetProperty(ref isRememberLogin, value); }
        }

        public bool IsLoading
        {
            get { return isLoading; }
            set { SetProperty(ref isLoading, value); }
        }

        public string ErrorMessage
        {
            get { return errorMessage; }
            set { SetProperty(ref errorMessage, value); }
        }

        public ICommand LoginCommand { get; private set; }

        public event Action LoginSuccess;

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(async p => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                ErrorMessage = "아이디를 입력하세요.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "비밀번호를 입력하세요.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = "";

                bool ok = await AuthApi.LoginAsync(UserId, Password);

                if (!ok)
                {
                    ErrorMessage = "아이디 또는 비밀번호가 올바르지 않습니다.";
                    return;
                }

                Action handler = LoginSuccess;

                if (handler != null)
                {
                    handler();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "로그인 중 오류가 발생했습니다.\n" + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}