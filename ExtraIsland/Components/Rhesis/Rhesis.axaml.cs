using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ExtraIsland.Shared;

namespace ExtraIsland.Components;

[ComponentInfo(
                  "FBB380C2-5480-4FED-8349-BA5F4EAD2688",
                  "名句一言",
                  "\uE3F4",
                  "显示一句古今名言,可使用三个API"
              )]
public partial class Rhesis : ComponentBase<RhesisConfig> {
    public Rhesis(ILessonsService lessonsService) {
        _authorLabel = new Label {
            Content = Title,
            Margin = new Thickness(0,4,0,0),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(_authorLabel,0);
        _authorLabel.Bind(FontSizeProperty,new DynamicResourceExtension("MainWindowSecondaryFontSize"));
        //_authorLabel.SetResourceReference(FontSizeProperty, "MainWindowSecondaryFontSize");

        _titleLabel = new Label {
            Content = Author,
            Margin = new Thickness(0,0,0,4),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(_titleLabel,1);
        _titleLabel.Bind(FontSizeProperty,new DynamicResourceExtension("MainWindowSecondaryFontSize"));
        //_titleLabel.SetResourceReference(FontSizeProperty, "MainWindowSecondaryFontSize");
        
        _infoGrid = new Grid {
            VerticalAlignment = VerticalAlignment.Center,
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children = {
                _authorLabel,
                _titleLabel
            }
        };
        
        LessonsService = lessonsService;
        InitializeComponent();
        _mainLabelAnimator = new Animators.GenericContentSwapAnimator(MainLabel);
        _subLabelAnimator = new Animators.GenericContentSwapAnimator(SubLabel);
    }

    ILessonsService LessonsService { get; }

    public string Showing { get; private set; } = "-----------------";
    public string Author { get; private set; } = "";
    public string Title { get; private set; } = "";
    readonly RhesisHandler.Instance _rhesisHandler = new RhesisHandler.Instance();
    readonly Animators.GenericContentSwapAnimator _mainLabelAnimator;
    readonly Animators.GenericContentSwapAnimator _subLabelAnimator;
    readonly Grid _infoGrid;
    readonly Label _titleLabel;
    readonly Label _authorLabel;
    
    private DispatcherTimer? _customContentTimer;
    
    void OnAttachedToVisualTree(object? sender,VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs) {
        Settings.LastUpdate = DateTime.Now;
        Update();
        LessonsService.PostMainTimerTicked += UpdateEvent;
        
        // 初始化自定义内容轮播定时器
        if (_customContentTimer == null) {
            _customContentTimer = new DispatcherTimer();
            _customContentTimer.Tick += OnCustomContentTimerTick;
        }
        
        // 设置定时器间隔
        if (Settings.IsCustomContentEnabled && Settings.CustomContentUpdateGapSeconds > 0) {
            _customContentTimer.Interval = Settings.CustomContentUpdateGap;
            _customContentTimer.Start();
        }
    }

    void OnDetachedFromVisualTree(object? sender,VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs) {
        LessonsService.PostMainTimerTicked -= UpdateEvent;
        
        // 停止并清理自定义内容定时器
        if (_customContentTimer != null) {
            _customContentTimer.Stop();
            _customContentTimer.Tick -= OnCustomContentTimerTick;
            _customContentTimer = null;
        }
    }

    void OnCustomContentTimerTick(object? sender, EventArgs e) {
        if (Settings.IsCustomContentEnabled) {
            UpdateCustomContent();
        }
    }

    void UpdateEvent(object? sender,EventArgs eventArgs) {
        // 只有在未启用自定义内容时才更新API内容
        if (!Settings.IsCustomContentEnabled) {
            if (EiUtils.GetDateTimeSpan(Settings.LastUpdate,DateTime.Now) < Settings.UpdateTimeGap
                | Settings.UpdateTimeGapSeconds == 0) return;
            Settings.LastUpdate = DateTime.Now;
            Update();
        }
    }

    void UpdateCustomContent() {
        string[] customContents = Settings.CustomContentList.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (customContents.Length == 0) return;
        
        // 更新索引，循环轮播
        int currentIndex = Settings.CurrentCustomIndex;
        string content = customContents[currentIndex].Trim();
        
        // 计算下一个索引
        int nextIndex = (currentIndex + 1) % customContents.Length;
        Settings.CurrentCustomIndex = nextIndex;
        
        // 解析自定义内容格式：内容|作者|出处
        string author = "";
        string title = "";
        string actualContent = content;
        
        int firstPipeIndex = content.IndexOf('|');
        if (firstPipeIndex > 0) {
            actualContent = content.Substring(0, firstPipeIndex).Trim();
            string remaining = content.Substring(firstPipeIndex + 1);
            
            int secondPipeIndex = remaining.IndexOf('|');
            if (secondPipeIndex > 0) {
                author = remaining.Substring(0, secondPipeIndex).Trim();
                title = remaining.Substring(secondPipeIndex + 1).Trim();
            } else {
                author = remaining.Trim();
            }
        }
        
        // 仅在内容不同时更新UI
        if (Showing != actualContent) {
            Showing = actualContent;
            Author = author;
            Title = title;
            
            object subObj;
            if (Settings.IsAuthorShowEnabled & Settings.IsTitleShowEnabled) {
                if (Settings.AttributesShowingInterval == 0) {
                    subObj = _infoGrid;   
                } else {
                    subObj = $"{Author} {Title}";
                }
            } else if (Settings.IsAuthorShowEnabled) {
                subObj = Author;
            } else if (Settings.IsTitleShowEnabled) {
                subObj = Title;
            } else {
                subObj = string.Empty;
            }
            
            Dispatcher.UIThread.InvokeAsync(() => {
                _titleLabel.Content = Title;
                _authorLabel.Content = Author;
                _mainLabelAnimator.Update(Showing, Settings.IsAnimationEnabled, Settings.IsSwapAnimationEnabled);
                
                if (Settings.IsAuthorShowEnabled | Settings.IsTitleShowEnabled) {
                    if (Settings.AttributesRule == RhesisConfig.AttributesDisplayRule.Sametime) {
                        SubLabel.IsVisible = true;
                        _subLabelAnimator.Update(subObj, Settings.IsAnimationEnabled, Settings.IsSwapAnimationEnabled);
                    } else {
                        SubLabel.IsVisible = false;
                        if (Math.Abs(Settings.AttributesShowingInterval - Settings.CustomContentUpdateGapSeconds) < 0.5) {
                            _mainLabelAnimator.Update(subObj, Settings.IsAnimationEnabled, false);
                            return;
                        }
                        new Thread(() => {
                            Thread.Sleep((int)((Settings.CustomContentUpdateGapSeconds - Settings.AttributesShowingInterval) * 1000));
                            _mainLabelAnimator.Update(subObj, Settings.IsAnimationEnabled, false);
                        }).Start();
                    }
                } else {
                    SubLabel.IsVisible = false;
                }
            });
        }
    }
    
    void Update() {
        Task.Run(() => {
            RhesisData data = _rhesisHandler.LegacyGet(Settings.DataSource,Settings.HitokotoProp switch {
                                                           "" => "https://v1.hitokoto.cn/",
                                                           _ => $"https://v1.hitokoto.cn/?{Settings.HitokotoLengthArgs}{Settings.HitokotoProp}"
                                                       },
                                                       Settings.SainticProp switch {
                                                           "" => "https://open.saintic.com/api/sentence/",
                                                           _ => $"https://open.saintic.com/api/sentence/{Settings.SainticProp}.json"
                                                       },
                                                       Settings.LengthLimitation);
            Showing = data.Content;
            Title = data.Title;
            Author = data.Author;
            if (Settings.IgnoreListString.Split("\r\n").Any(keyWord => Showing.Contains(keyWord) && keyWord != "")) return;
            object subObj;
            if (Settings.IsAuthorShowEnabled & Settings.IsTitleShowEnabled) {
                if (Settings.AttributesShowingInterval == 0) {
                    subObj = _infoGrid;   
                } else {
                    subObj = $"{Author} {Title}";
                }
            } else if (Settings.IsAuthorShowEnabled) {
                subObj = Author;
            } else if (Settings.IsTitleShowEnabled) {
                subObj = Title;
            } else {
                subObj = string.Empty;
            }
            Dispatcher.UIThread.InvokeAsync(() => {
                _titleLabel.Content = Title;
                _authorLabel.Content = Author;
                _mainLabelAnimator.Update(Showing,Settings.IsAnimationEnabled,Settings.IsSwapAnimationEnabled);
                if (Settings.IsAuthorShowEnabled | Settings.IsTitleShowEnabled) {
                    if (Settings.AttributesRule == RhesisConfig.AttributesDisplayRule.Sametime) {
                        SubLabel.IsVisible =  true;
                        _subLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,Settings.IsSwapAnimationEnabled);
                    } else {
                        SubLabel.IsVisible = false;
                        if (Math.Abs(Settings.AttributesShowingInterval - Settings.UpdateTimeGapSeconds) < 0.5) {
                            _mainLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,false);
                            return;
                        }
                        new Thread(() => {
                            Thread.Sleep((int)((Settings.UpdateTimeGapSeconds - Settings.AttributesShowingInterval) * 1000));
                            _mainLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,false);
                        }).Start();
                    }
                } else {
                    SubLabel.IsVisible =  false;
                }
            });
        });
    }
}
