using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace 코빚노_프로젝트
{
    public partial class FriendWindow : Window
    {
        private readonly string _loginUserId;
        private readonly int _memberId;

        private int _chatRoomId = 0;
        private bool _isLoadedMessage = false;

        private DispatcherTimer _chatRefreshTimer;
        private bool _isRefreshingMessages = false;

        private readonly HashSet<string> _renderedMessageKeys =
            new HashSet<string>();

        private readonly HashSet<string> _participants =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private const string TOUR_SHARE_PREFIX = "TOUR_SHARE_JSON:";
        private const string CHAT_TEXT_PREFIX = "CHAT_TEXT_JSON:";

        private class TourSharePayload
        {
            public int SenderId { get; set; }
            public string SenderName { get; set; }

            public int SpotId { get; set; }
            public string SpotName { get; set; }
            public string SpotAddress { get; set; }
            public string SpotCategory { get; set; }
            public string SpotImageUrl { get; set; }

            public double MapX { get; set; }
            public double MapY { get; set; }

            public string Rating { get; set; }
            public string Icon { get; set; }
        }

        private class ChatTextPayload
        {
            public int SenderId { get; set; }
            public string SenderName { get; set; }
            public string Text { get; set; }
        }

        public FriendWindow()
            : this("", 1)
        {
        }

        public FriendWindow(string loginUserId)
            : this(loginUserId, 1)
        {
        }

        public FriendWindow(string loginUserId, int memberId)
        {
            InitializeComponent();

            _loginUserId = loginUserId;
            _memberId = memberId <= 0 ? 1 : memberId;

            if (!string.IsNullOrWhiteSpace(_loginUserId))
            {
                TxtRoomSub.Text = _loginUserId + "님의 여행 그룹";
            }

            Loaded += FriendWindow_Loaded;

            Closed += (s, e) =>
            {
                StopChatRefreshTimer();
            };
        }

        private async void FriendWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoadedMessage)
                return;

            _isLoadedMessage = true;

            await InitChatAsync();
        }

        private async Task InitChatAsync()
        {
            try
            {
                ChatRoomResponse room = await ChatApi.GetRoomAsync();

                if (room == null)
                {
                    MessagePanel.Children.Clear();
                    AddDateMessage(DateTime.Now.ToString("yyyy년 M월 d일"));
                    AddSystemMessage("채팅방을 불러오지 못했습니다");
                    return;
                }

                _chatRoomId = room.GetRoomId();

                if (!string.IsNullOrWhiteSpace(room.RoomName))
                {
                    TxtRoomSub.Text = room.RoomName;
                }

                MessagePanel.Children.Clear();
                _renderedMessageKeys.Clear();

                AddDateMessage(DateTime.Now.ToString("yyyy년 M월 d일"));

                RegisterParticipant(GetMySenderName(), _memberId);
                UpdateParticipantCount();

                await RefreshMessagesAsync(true);

                StartChatRefreshTimer();
            }
            catch (Exception ex)
            {
                MessagePanel.Children.Clear();
                AddDateMessage(DateTime.Now.ToString("yyyy년 M월 d일"));
                AddSystemMessage("채팅 서버 연결 실패");
                MessageBox.Show(ex.ToString(), "채팅 초기화 오류");
            }
        }

        private async Task RefreshMessagesAsync(bool showEmptyMessage)
        {
            if (_chatRoomId <= 0)
                return;

            if (_isRefreshingMessages)
                return;

            _isRefreshingMessages = true;

            try
            {
                List<ChatMessageResponse> messages =
                    await ChatApi.GetMessagesAsync(_chatRoomId);

                RefreshParticipantCountFromMessages(messages);

                if (messages == null || messages.Count == 0)
                {
                    if (showEmptyMessage && MessagePanel.Children.Count <= 1)
                    {
                        AddSystemMessage("공유한 관광지가 이 채팅방에 표시됩니다");
                    }

                    return;
                }

                int addedCount = 0;

                foreach (ChatMessageResponse msg in messages)
                {
                    string key = GetMessageKey(msg);

                    if (_renderedMessageKeys.Contains(key))
                        continue;

                    _renderedMessageKeys.Add(key);
                    RenderServerMessage(msg);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    ScrollBottom();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                _isRefreshingMessages = false;
            }
        }
        private string GetMessageKey(ChatMessageResponse msg)
        {
            if (msg == null)
                return Guid.NewGuid().ToString();

            if (msg.ChatMessageId > 0)
                return "chat_" + msg.ChatMessageId;

            if (msg.MessageId > 0)
                return "msg_" + msg.MessageId;

            string text = FirstText(
                msg.Message,
                msg.Content,
                msg.SpotName,
                msg.TargetName,
                ""
            );

            string timeKey = "";

            if (msg.CreatedAt != null)
                timeKey = msg.CreatedAt.Value.Ticks.ToString();

            return msg.ChatRoomId + "_" + msg.SenderId + "_" + timeKey + "_" + text;
        }

        private void RenderServerMessage(ChatMessageResponse msg)
        {
            if (msg == null)
                return;

            string time = FormatTime(msg.CreatedAt);
            string text = FirstText(msg.Message, msg.Content, "");

            ChatTextPayload chatText;

            if (TryParseChatTextMessage(text, out chatText))
            {
                bool isMineText = IsMineSender(chatText.SenderId, chatText.SenderName);

                if (isMineText)
                {
                    AddMineText(chatText.Text, time);
                }
                else
                {
                    string name = FirstText(chatText.SenderName, "회원 " + chatText.SenderId);

                    AddOtherText(
                        name,
                        chatText.Text,
                        time,
                        Hex("#F0F0F0"),
                        Hex("#333333")
                    );
                }

                return;
            }

            TourSharePayload sharedTour;

            if (TryParseTourShareMessage(text, out sharedTour))
            {
                bool isMineShare = IsMineSender(sharedTour.SenderId, sharedTour.SenderName);

                if (isMineShare)
                {
                    AddMinePlace(
                        sharedTour.SpotName,
                        sharedTour.SpotAddress,
                        sharedTour.SpotCategory,
                        sharedTour.Rating,
                        "공유",
                        GetCategoryBackground(sharedTour.SpotCategory),
                        sharedTour.Icon,
                        sharedTour.SpotImageUrl,
                        sharedTour.MapX,
                        sharedTour.MapY,
                        time
                    );
                }
                else
                {
                    string name = FirstText(sharedTour.SenderName, "회원 " + sharedTour.SenderId);

                    AddOtherPlace(
                        name,
                        sharedTour.SpotName,
                        sharedTour.SpotAddress,
                        sharedTour.SpotCategory,
                        sharedTour.Rating,
                        "공유",
                        GetCategoryBackground(sharedTour.SpotCategory),
                        sharedTour.Icon,
                        sharedTour.SpotImageUrl,
                        sharedTour.MapX,
                        sharedTour.MapY,
                        time,
                        Hex("#F0F0F0"),
                        Hex("#333333")
                    );
                }

                return;
            }

            if (IsServerSpotMessage(msg))
            {
                string senderName = FirstText(
                    msg.SenderName,
                    msg.MemberName,
                    msg.UserId,
                    "회원 " + msg.SenderId
                );

                bool isMineSpot = IsMineSender(msg.SenderId, senderName);

                string placeName = FirstText(
                    msg.SpotName,
                    msg.TargetName,
                    msg.TouristSpotName,
                    msg.PlaceName,
                    "장소명 없음"
                );

                string address = FirstText(
                    msg.SpotAddress,
                    msg.Address,
                    "주소 정보 없음"
                );

                string category = FirstText(
                    msg.Category,
                    "관광지"
                );

                string rating = "0.0";

                if (msg.Rating != null)
                    rating = msg.Rating.Value.ToString("0.0");
                else if (!string.IsNullOrWhiteSpace(msg.RatingText))
                    rating = msg.RatingText;

                string count = FirstText(msg.ReviewCountText, "공유");

                string icon = GetCategoryIcon(category);
                Brush bg = GetCategoryBackground(category);

                if (isMineSpot)
                {
                    AddMinePlace(
                        placeName,
                        address,
                        category,
                        rating,
                        count,
                        bg,
                        icon,
                        msg.SpotImageUrl,
                        0,
                        0,
                        time
                    );
                }
                else
                {
                    AddOtherPlace(
                        senderName,
                        placeName,
                        address,
                        category,
                        rating,
                        count,
                        bg,
                        icon,
                        msg.SpotImageUrl,
                        0,
                        0,
                        time,
                        Hex("#F0F0F0"),
                        Hex("#333333")
                    );
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(text))
                return;

            string fallbackSenderName = FirstText(
                msg.SenderName,
                msg.MemberName,
                msg.UserId,
                "회원 " + msg.SenderId
            );

            bool isMine =
                IsMineSender(msg.SenderId, fallbackSenderName);

            if (isMine)
            {
                AddMineText(text, time);
            }
            else
            {
                AddOtherText(
                    fallbackSenderName,
                    text,
                    time,
                    Hex("#F0F0F0"),
                    Hex("#333333")
                );
            }
        }

        private bool TryParseChatTextMessage(string text, out ChatTextPayload payload)
        {
            payload = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!text.StartsWith(CHAT_TEXT_PREFIX))
                return false;

            try
            {
                string json = text.Substring(CHAT_TEXT_PREFIX.Length);

                payload = JsonConvert.DeserializeObject<ChatTextPayload>(json);

                if (payload == null)
                    return false;

                if (string.IsNullOrWhiteSpace(payload.SenderName))
                    payload.SenderName = "회원 " + payload.SenderId;

                if (payload.Text == null)
                    payload.Text = "";

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseTourShareMessage(string text, out TourSharePayload payload)
        {
            payload = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!text.StartsWith(TOUR_SHARE_PREFIX))
                return false;

            try
            {
                string json = text.Substring(TOUR_SHARE_PREFIX.Length);

                payload = JsonConvert.DeserializeObject<TourSharePayload>(json);

                if (payload == null)
                    return false;

                if (string.IsNullOrWhiteSpace(payload.SenderName))
                    payload.SenderName = "회원 " + payload.SenderId;

                if (string.IsNullOrWhiteSpace(payload.SpotName))
                    payload.SpotName = "장소명 없음";

                if (string.IsNullOrWhiteSpace(payload.SpotAddress))
                    payload.SpotAddress = "주소 정보 없음";

                if (string.IsNullOrWhiteSpace(payload.SpotCategory))
                    payload.SpotCategory = "관광지";

                if (string.IsNullOrWhiteSpace(payload.Rating))
                    payload.Rating = "0.0";

                if (string.IsNullOrWhiteSpace(payload.Icon))
                    payload.Icon = "📍";

                if (string.IsNullOrWhiteSpace(payload.SpotImageUrl))
                    payload.SpotImageUrl = "";

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMineSender(int senderId, string senderName)
        {
            string myName = NormalizeUserName(_loginUserId);
            string otherName = NormalizeUserName(senderName);

            if (!string.IsNullOrWhiteSpace(myName) &&
                !string.IsNullOrWhiteSpace(otherName))
            {
                return myName == otherName;
            }

            if (senderId > 0 && senderId == _memberId)
                return true;

            return false;
        }

        private bool IsServerSpotMessage(ChatMessageResponse msg)
        {
            if (msg == null)
                return false;

            if (msg.SpotId != null && msg.SpotId.Value > 0)
                return true;

            if (!string.IsNullOrWhiteSpace(msg.SpotName))
                return true;

            if (!string.IsNullOrWhiteSpace(msg.SpotAddress))
                return true;

            if (!string.IsNullOrWhiteSpace(msg.SpotImageUrl))
                return true;

            if (msg.TargetId != null && msg.TargetId.Value > 0)
                return true;

            if (!string.IsNullOrWhiteSpace(msg.TargetName))
                return true;

            if (!string.IsNullOrWhiteSpace(msg.TouristSpotName))
                return true;

            if (!string.IsNullOrWhiteSpace(msg.PlaceName))
                return true;

            return false;
        }

        public async void ShareTourFromMain(TourSpotItem tour)
        {
            if (tour == null)
                return;

            try
            {
                if (_chatRoomId <= 0)
                {
                    ChatRoomResponse room = await ChatApi.GetRoomAsync();

                    if (room == null)
                    {
                        MessageBox.Show("채팅방 정보를 가져오지 못했습니다.");
                        return;
                    }

                    _chatRoomId = room.GetRoomId();
                }

                int targetId = 0;
                int.TryParse(tour.ContentId, out targetId);

                TourSharePayload payload = new TourSharePayload
                {
                    SenderId = _memberId,
                    SenderName = GetMySenderName(),

                    SpotId = targetId,
                    SpotName = FirstText(tour.Name, "장소명 없음"),
                    SpotAddress = FirstText(tour.Address, "주소 정보 없음"),
                    SpotCategory = FirstText(tour.Category, "관광지"),
                    SpotImageUrl = tour.ImageUrl,

                    MapX = tour.MapX,
                    MapY = tour.MapY,

                    Rating = string.IsNullOrWhiteSpace(tour.Rating)
                        ? "0.0"
                        : tour.Rating.Replace("★", "").Trim(),

                    Icon = string.IsNullOrWhiteSpace(tour.Icon) ? "📍" : tour.Icon
                };

                string json = JsonConvert.SerializeObject(payload);
                string sendText = TOUR_SHARE_PREFIX + json;

                ChatMessageRequest request = new ChatMessageRequest
                {
                    ChatRoomId = _chatRoomId,

                    SenderId = _memberId,
                    MemberId = _memberId,

                    Message = sendText,
                    Content = sendText
                };

                bool ok = await ChatApi.AddMessageAsync(request);

                if (!ok)
                    return;

                await RefreshMessagesAsync(false);
                ScrollBottom();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "관광지 공유 오류");
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendTextMessageAsync();
        }

        private async void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SendTextMessageAsync();
            }
        }

        private async Task SendTextMessageAsync()
        {
            string text = TxtMessage.Text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                if (_chatRoomId <= 0)
                {
                    ChatRoomResponse room = await ChatApi.GetRoomAsync();

                    if (room == null)
                    {
                        MessageBox.Show("채팅방 정보를 가져오지 못했습니다.");
                        return;
                    }

                    _chatRoomId = room.GetRoomId();
                }

                ChatTextPayload payload = new ChatTextPayload
                {
                    SenderId = _memberId,
                    SenderName = GetMySenderName(),
                    Text = text
                };

                string sendText = CHAT_TEXT_PREFIX + JsonConvert.SerializeObject(payload);

                ChatMessageRequest request = new ChatMessageRequest
                {
                    ChatRoomId = _chatRoomId,

                    SenderId = _memberId,
                    MemberId = _memberId,

                    Message = sendText,
                    Content = sendText
                };

                bool ok = await ChatApi.AddMessageAsync(request);

                if (!ok)
                    return;

                TxtMessage.Text = "";
                await RefreshMessagesAsync(false);
                ScrollBottom();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "메시지 전송 오류");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            StopChatRefreshTimer();
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch(Exception ex) 
                {
                    Debug.WriteLine(ex.ToString());

                }
            }
        }

        private void BtnToggleSharePopup_Click(object sender, RoutedEventArgs e)
        {
            ToggleSharePopup();
        }

        private void ToggleSharePopup()
        {
            if (SharePopup.Visibility == Visibility.Visible)
                SharePopup.Visibility = Visibility.Collapsed;
            else
                SharePopup.Visibility = Visibility.Visible;
        }

        private async void BtnSharePlace_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == null)
                return;

            string tag = btn.Tag as string;

            if (string.IsNullOrWhiteSpace(tag))
                return;

            string[] sp = tag.Split('|');

            if (sp.Length < 7)
                return;

            string name = sp[0];
            string address = sp[1];
            string category = sp[2];
            string rating = sp[3];
            string bg = sp[5];
            string icon = sp[6];

            SharePopup.Visibility = Visibility.Collapsed;

            TourSharePayload payload = new TourSharePayload
            {
                SenderId = _memberId,
                SenderName = GetMySenderName(),

                SpotId = 0,
                SpotName = name,
                SpotAddress = address,
                SpotCategory = category,
                SpotImageUrl = "",
                MapX = 0,
                MapY = 0,
                Rating = rating,
                Icon = icon
            };

            string sendText = TOUR_SHARE_PREFIX + JsonConvert.SerializeObject(payload);

            if (_chatRoomId > 0)
            {
                ChatMessageRequest request = new ChatMessageRequest
                {
                    ChatRoomId = _chatRoomId,
                    SenderId = _memberId,
                    MemberId = _memberId,
                    Message = sendText,
                    Content = sendText
                };

                bool ok = await ChatApi.AddMessageAsync(request);

                if (ok)
                {
                    await RefreshMessagesAsync(false);
                    ScrollBottom();
                }
            }
        }

        private void StartChatRefreshTimer()
        {
            if (_chatRefreshTimer != null)
                return;

            _chatRefreshTimer = new DispatcherTimer();
            _chatRefreshTimer.Interval = TimeSpan.FromSeconds(1);

            _chatRefreshTimer.Tick += async (s, e) =>
            {
                await RefreshMessagesAsync(false);
            };

            _chatRefreshTimer.Start();
        }

        private void StopChatRefreshTimer()
        {
            if (_chatRefreshTimer == null)
                return;

            _chatRefreshTimer.Stop();
            _chatRefreshTimer = null;
        }

        private void AddDateMessage(string text)
        {
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 2, 0, 2);

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border left = new Border
            {
                Height = 1,
                Background = Hex("#EEEEEE"),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock center = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Hex("#BBBBBB"),
                Margin = new Thickness(8, 0, 8, 0)
            };

            Border right = new Border
            {
                Height = 1,
                Background = Hex("#EEEEEE"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(left, 0);
            Grid.SetColumn(center, 1);
            Grid.SetColumn(right, 2);

            grid.Children.Add(left);
            grid.Children.Add(center);
            grid.Children.Add(right);

            MessagePanel.Children.Add(grid);
        }

        private void AddSystemMessage(string text)
        {
            Border border = new Border
            {
                Background = Hex("#F5F5F5"),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(12, 3, 12, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };

            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Hex("#BBBBBB")
            };

            MessagePanel.Children.Add(border);
        }

        private void AddOtherText(string senderName, string message, string time, Brush avatarBg, Brush avatarFg)
        {
            StackPanel row = CreateOtherRow(senderName, avatarBg, avatarFg);

            StackPanel col = row.Children[1] as StackPanel;

            col.Children.Add(CreateNameBlock(senderName));
            col.Children.Add(CreateBubble(message, false));
            col.Children.Add(CreateTimeBlock(time, false));

            MessagePanel.Children.Add(row);
        }

        private void AddMineText(string message, string time)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 5),
                MaxWidth = 360
            };

            StackPanel col = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            col.Children.Add(CreateMineNameBlock("나"));
            col.Children.Add(CreateBubble(message, true));
            col.Children.Add(CreateTimeBlock(time, true));

            row.Children.Add(col);
            row.Children.Add(CreateAvatarCircle(Hex("#FFE500"), Hex("#333333"), true));

            MessagePanel.Children.Add(row);
        }

        private void AddOtherPlace(string senderName, string placeName, string address, string category,
            string rating, string count, Brush thumbBg, string icon, string imageUrl,
            double mapX, double mapY, string time, Brush avatarBg, Brush avatarFg)
        {
            StackPanel row = CreateOtherRow(senderName, avatarBg, avatarFg);

            StackPanel col = row.Children[1] as StackPanel;

            col.Children.Add(CreateNameBlock(senderName));
            col.Children.Add(CreatePlaceCard(placeName, address, category, rating, count, thumbBg, icon, imageUrl, mapX, mapY));
            col.Children.Add(CreateTimeBlock(time, false));

            MessagePanel.Children.Add(row);
        }

        private void AddMinePlace(string placeName, string address, string category,
            string rating, string count, Brush thumbBg, string icon, string imageUrl,
            double mapX, double mapY, string time)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 5),
                MaxWidth = 360
            };

            StackPanel col = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            col.Children.Add(CreateMineNameBlock("나"));
            col.Children.Add(CreatePlaceCard(placeName, address, category, rating, count, thumbBg, icon, imageUrl, mapX, mapY));
            col.Children.Add(CreateTimeBlock(time, true));

            row.Children.Add(col);
            row.Children.Add(CreateAvatarCircle(Hex("#FFE500"), Hex("#333333"), true));

            MessagePanel.Children.Add(row);
        }

        private StackPanel CreateOtherRow(string senderName, Brush avatarBg, Brush avatarFg)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5),
                MaxWidth = 360
            };

            row.Children.Add(CreateAvatarCircle(avatarBg, avatarFg, false));

            StackPanel col = new StackPanel();

            row.Children.Add(col);

            return row;
        }

        private Border CreateAvatarCircle(Brush bg, Brush fg, bool isMine)
        {
            Border avatarBox = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = bg,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = isMine
                    ? new Thickness(7, 18, 0, 0)
                    : new Thickness(0, 18, 7, 0)
            };

            Grid grid = new Grid();

            TextBlock person = new TextBlock
            {
                Text = "👤",
                FontSize = 13,
                Foreground = fg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            grid.Children.Add(person);

            avatarBox.Child = grid;

            return avatarBox;
        }

        private TextBlock CreateNameBlock(string name)
        {
            return new TextBlock
            {
                Text = name,
                FontSize = 11,
                Foreground = Hex("#888888"),
                Margin = new Thickness(3, 0, 0, 3)
            };
        }

        private TextBlock CreateMineNameBlock(string name)
        {
            return new TextBlock
            {
                Text = name,
                FontSize = 11,
                Foreground = Hex("#888888"),
                Margin = new Thickness(0, 0, 3, 3),
                HorizontalAlignment = HorizontalAlignment.Right
            };
        }

        private Border CreateBubble(string text, bool isMine)
        {
            Border bubble = new Border
            {
                Background = isMine ? Hex("#FFE500") : Hex("#F2F2F2"),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 250
            };

            if (isMine)
                bubble.CornerRadius = new CornerRadius(14, 4, 14, 14);
            else
                bubble.CornerRadius = new CornerRadius(4, 14, 14, 14);

            bubble.Child = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = Hex("#1A1A1A"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            return bubble;
        }

        private TextBlock CreateTimeBlock(string time, bool isMine)
        {
            return new TextBlock
            {
                Text = time,
                FontSize = 10,
                Foreground = Hex("#CCCCCC"),
                Margin = new Thickness(3, 3, 3, 0),
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
        }

        private Border CreatePlaceCard(string name, string address, string category, string rating, string count,
            Brush thumbBg, string icon, string imageUrl, double mapX, double mapY)
        {
            Border card = new Border
            {
                Width = 210,
                Background = Brushes.White,
                BorderBrush = Hex("#FFE500"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true
            };

            StackPanel root = new StackPanel();

            Grid thumb = new Grid
            {
                Height = 95,
                Background = thumbBg,
                ClipToBounds = true
            };

            bool imageLoaded = false;

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();

                    Image image = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.UniformToFill
                    };

                    thumb.Children.Add(image);
                    imageLoaded = true;
                }
                catch
                {
                    imageLoaded = false;
                }
            }

            if (!imageLoaded)
            {
                TextBlock iconText = new TextBlock
                {
                    Text = icon,
                    FontSize = 30,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                thumb.Children.Add(iconText);
            }

            Border tag = new Border
            {
                Background = Hex("#FFE500"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(6)
            };

            tag.Child = new TextBlock
            {
                Text = category,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Hex("#1A1A1A")
            };

            thumb.Children.Add(tag);

            StackPanel body = new StackPanel
            {
                Margin = new Thickness(10, 8, 10, 10)
            };

            body.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Hex("#1A1A1A"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            body.Children.Add(new TextBlock
            {
                Text = "📍 " + address,
                FontSize = 11,
                Foreground = Hex("#888888"),
                Margin = new Thickness(0, 4, 0, 8),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid footer = new Grid();

            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock ratingText = new TextBlock
            {
                Text = "★ " + rating + " (" + count + ")",
                FontSize = 11,
                Foreground = Hex("#FFB800"),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button mapButton = new Button
            {
                Content = "지도 보기",
                Background = Hex("#FFE500"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(9, 3, 9, 3),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };

            mapButton.Click += (s, e) =>
            {
                e.Handled = true;
                OpenPlaceOnMap(name, address, mapX, mapY);
            };

            Grid.SetColumn(ratingText, 0);
            Grid.SetColumn(mapButton, 1);

            footer.Children.Add(ratingText);
            footer.Children.Add(mapButton);

            body.Children.Add(footer);

            root.Children.Add(thumb);
            root.Children.Add(body);

            card.Child = root;

            return card;
        }

        private void OpenPlaceOnMap(string name, string address, double mapX, double mapY)
        {
            try
            {
                MainWindow main = Owner as MainWindow;

                if (main != null)
                {
                    main.ShowSharedPlaceOnMap(name, address, mapX, mapY);

                    StopChatRefreshTimer();

                    // 친구창이 지도 위를 덮고 있으니까 닫아서 지도 보이게 함
                    Close();

                    main.Activate();
                    return;
                }

                OpenKakaoMapSearch(name, address);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "지도 보기 오류");
            }
        }

        private void OpenKakaoMapSearch(string name, string address)
        {
            string query = name + " " + address;
            string url = "https://map.kakao.com/link/search/" + Uri.EscapeDataString(query);

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private string GetNowText()
        {
            DateTime now = DateTime.Now;

            string ampm = now.Hour >= 12 ? "오후" : "오전";
            int hour = now.Hour % 12;

            if (hour == 0)
                hour = 12;

            return ampm + " " + hour + ":" + now.Minute.ToString("00");
        }

        private string FormatTime(DateTime? dateTime)
        {
            if (dateTime == null)
                return GetNowText();

            DateTime time = dateTime.Value;

            string ampm = time.Hour >= 12 ? "오후" : "오전";
            int hour = time.Hour % 12;

            if (hour == 0)
                hour = 12;

            return ampm + " " + hour + ":" + time.Minute.ToString("00");
        }

        private string FirstText(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private Brush GetCategoryBackground(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Hex("#FFF8CC");

            if (category.Contains("문화") || category.Contains("유적") || category.Contains("궁"))
                return Hex("#DDEEFF");

            if (category.Contains("쇼핑"))
                return Hex("#FFF8CC");

            if (category.Contains("맛집") || category.Contains("음식"))
                return Hex("#FCE8E8");

            if (category.Contains("국립") || category.Contains("공원") || category.Contains("여행"))
                return Hex("#E8F5E9");

            if (category.Contains("해변") || category.Contains("바다"))
                return Hex("#E8F4FF");

            return Hex("#FFF8CC");
        }

        private string GetCategoryIcon(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "📍";

            if (category.Contains("문화") || category.Contains("유적") || category.Contains("궁"))
                return "🏛️";

            if (category.Contains("쇼핑"))
                return "🛍️";

            if (category.Contains("맛집") || category.Contains("음식"))
                return "🍜";

            if (category.Contains("국립") || category.Contains("공원") || category.Contains("여행"))
                return "🏔️";

            if (category.Contains("해변") || category.Contains("바다"))
                return "🏖️";

            return "📍";
        }

        private SolidColorBrush Hex(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        private void ScrollBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MsgScrollViewer.ScrollToEnd();
            }), DispatcherPriority.Background);
        }

        private void RefreshParticipantCountFromMessages(List<ChatMessageResponse> messages)
        {
            _participants.Clear();

            RegisterParticipant(GetMySenderName(), _memberId);

            if (messages != null)
            {
                foreach (ChatMessageResponse msg in messages)
                {
                    RegisterParticipantFromMessage(msg);
                }
            }

            UpdateParticipantCount();
        }

        private void RegisterParticipantFromMessage(ChatMessageResponse msg)
        {
            if (msg == null)
                return;

            string text = FirstText(msg.Message, msg.Content, "");

            ChatTextPayload chatText;

            if (TryParseChatTextMessage(text, out chatText))
            {
                RegisterParticipant(chatText.SenderName, chatText.SenderId);
                return;
            }

            TourSharePayload tourShare;

            if (TryParseTourShareMessage(text, out tourShare))
            {
                RegisterParticipant(tourShare.SenderName, tourShare.SenderId);
                return;
            }

            string senderName = FirstText(
                msg.SenderName,
                msg.MemberName,
                msg.UserId,
                msg.SenderId > 0 ? "회원 " + msg.SenderId : ""
            );

            int senderId = msg.SenderId;

            if (senderId <= 0)
                senderId = msg.MemberId;

            RegisterParticipant(senderName, senderId);
        }

        private void RegisterParticipant(string senderName, int senderId)
        {
            string key = NormalizeUserName(senderName);

            if (string.IsNullOrWhiteSpace(key) && senderId > 0)
                key = "회원 " + senderId;

            if (string.IsNullOrWhiteSpace(key))
                return;

            _participants.Add(key);
        }

        private void UpdateParticipantCount()
        {
            if (TxtParticipantCount == null)
                return;

            int count = _participants.Count;

            if (count <= 0)
                count = 1;

            TxtParticipantCount.Text = count + "명";
        }

        private string NormalizeUserName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            return name
                .Replace("👤", "")
                .Replace("님", "")
                .Trim()
                .ToLower();
        }

        private string GetMySenderName()
        {
            if (!string.IsNullOrWhiteSpace(_loginUserId))
                return _loginUserId;

            return "회원 " + _memberId;
        }
    }
}