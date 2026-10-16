using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinTendedero.Core;
using WinTendedero.Interop;

namespace WinTendedero.UI;

/// <summary>
/// One photo with its clothespin. All the charm lives here: it drops onto
/// the line, swings, sways with the breeze, and reacts to the pointer.
/// Click copies, press and hold opens the editor, drag takes it out, the
/// cross in the corner discards.
/// </summary>
public sealed class PeggedControl : Canvas
{
    public Pegged Item { get; }
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);
    private sealed class CardPeer(PeggedControl owner):System.Windows.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore()=>System.IO.Path.GetFileName(owner.Item.Path);
        protected override string GetAutomationIdCore()=>"Capture_"+owner.Item.Id;
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore()=>System.Windows.Automation.Peers.AutomationControlType.Group;
        protected override Rect GetBoundingRectangleCore()
        {
            if(PresentationSource.FromVisual(owner) is not { } source)return Rect.Empty;
            var center=owner.PointToScreen(new Point(Layout.CardWidth/2,Layout.CardOffsetBelowTop+owner._cardSize.Height/2));
            var scale=source.CompositionTarget.TransformToDevice.M11;
            return new Rect(center.X-owner._cardSize.Width*scale/2,center.Y-owner._cardSize.Height*scale/2,owner._cardSize.Width*scale,owner._cardSize.Height*scale);
        }
    }
    private readonly Line _line;

    // Motion
    private readonly Spring _swing = Spring.Raw(80, 18-9*MotionFeel.Amount);
    private readonly Spring _arrive = MotionFeel.Spring(.36, -36);
    private readonly Spring _hoverScale = MotionFeel.Spring(.28, 1);
    private readonly Spring _pull = MotionFeel.Spring(.30);
    private readonly Tween _pressScale = new(1);
    private readonly Spring _x = MotionFeel.Spring(.38);
    private readonly Tween _opacity = new(0);
    private readonly Tween _cardOpacity = new(1);
    private readonly Tween _crossOpacity = new(0);
    private readonly Tween _crossScale = new(0.6);
    private readonly Tween _copiedOpacity = new(0);
    private readonly Tween _copiedOffset = new(-4);
    private readonly Tween _shadow = new(0);                                    // 0 resting, 1 hovering

    // Visuals
    private readonly RotateTransform _rotate = new();
    private readonly TranslateTransform _drop = new();
    private readonly TranslateTransform _position = new();
    private readonly ScaleTransform _cardScale = new();
    private readonly Grid _cardHost;
    private readonly PhotoFrame _frame;
    private readonly Image _image;
    private readonly Border _cross;
    private readonly Border _pen;
    private readonly Border _copied;
    private readonly TranslateTransform _copiedTranslate = new();
    private readonly DropShadowEffect _shadowEffect;
    private Grid _pin = null!;
    private bool _reordering;
    private double _grabOffsetX;
    private Point _downInCanvas;
    private double _dragTime, _lastDragX, _dragVelocity;
    private readonly TextBlock _copiedText;
    private readonly TextBlock _tipName = new();
    private readonly TextBlock _tipMeta = new();
    private Size _cardSize;

    // Input
    private Point? _downPoint;
    private bool _startedDrag, _didLongPress, _hovering, _gestureMoved;
    private readonly DispatcherTimer _holdTimer;
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(450);

    public bool Hovering => _hovering;
    public bool Reordering => _reordering;
    public bool Active => !_swing.Resting || !_arrive.Resting || !_hoverScale.Resting || !_pressScale.Done || !_x.Resting
                          || !_opacity.Done || !_cardOpacity.Done || !_crossOpacity.Done || !_copiedOpacity.Done || !_shadow.Done || !_pull.Resting;

    public PeggedControl(Pegged item, Line line)
    {
        Item = item;
        _line = line;
        Width = Layout.CardWidth;
        ClipToBounds = false;
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;
        Background = null;

        // The whole hanging thing rotates about the top of the clip, where it grips the line.
        var group = new TransformGroup();
        group.Children.Add(_rotate);
        group.Children.Add(_drop);
        group.Children.Add(_position);
        RenderTransform = group;
        _rotate.CenterX = Layout.CardWidth / 2;
        _rotate.CenterY = 0;

        // Card
        _cardSize = Layout.CardSize(item.PixelWidth, item.PixelHeight);
        _shadowEffect = new DropShadowEffect { Color = Colors.Black, Direction = 270, ShadowDepth = 3, BlurRadius = 12, Opacity = 0.14 };
        _cardHost = new Grid
        {
            Width = _cardSize.Width, Height = _cardSize.Height,
            RenderTransform = _cardScale,
            CacheMode = new BitmapCache { RenderAtScale = 1.5, SnapsToDevicePixels = true },
            Effect = _shadowEffect,
            Background = Brushes.Transparent,
            Cursor = Cursors.Arrow,
        };
        _cardScale.CenterX = _cardSize.Width / 2;
        _cardScale.CenterY = 0;

        _frame = new PhotoFrame();
        _image = new Image
        {
            Source = item.Thumb,
            Stretch = Stretch.Fill,
            Margin = PhotoFrames.Insets,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        ApplyPhotoClip();
        var photoEdge = new Border
        {
            Margin = PhotoFrames.Insets,
            CornerRadius = new CornerRadius(PhotoFrames.PhotoRadius),
            BorderThickness = new Thickness(0.5),
            BorderBrush = Theme.Freeze(new SolidColorBrush(Theme.PhotoEdge)),
            IsHitTestVisible = false,
        };
        var outline = Glass.Outline(Layout.FrameRadius);
        _cardHost.Children.Add(_frame);
        _cardHost.Children.Add(_image);
        _cardHost.Children.Add(photoEdge);
        _cardHost.Children.Add(outline);
        if (Notes.IsNote(item.Path))
        {
            // A note is the paper itself: no glass, no inset.
            _frame.Visibility = photoEdge.Visibility = outline.Visibility = Visibility.Collapsed;
            _image.Margin = new Thickness(0);
        }

        // The discard cross in the top left corner.
        _cross = new Border
        {
            Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(3),
            BorderThickness = new Thickness(0.75),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.6, 0.6),
            Cursor = Cursors.Hand,
            Child = Glass.Cross(8, Theme.Freeze(new SolidColorBrush(Theme.Primary))),
        };
        Glass.Paint(_cross);
        _cross.MouseLeftButtonDown += (_, e) => { e.Handled = true; _downPoint = null; _line.Discard(Item.Id); };
        _cardHost.Children.Add(_cross);

        // The pen in the top right corner: mark the photo up.
        _pen = new Border
        {
            Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(3),
            BorderThickness = new Thickness(0.75),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.6, 0.6),
            Cursor = Cursors.Hand,
            ToolTip = Strings.MarkUp,
            Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M1,9 L7,3 L9,5 L3,11 Z M0,12 L3,11"),
                Stroke = Theme.Freeze(new SolidColorBrush(Theme.Primary)), StrokeThickness = 1.4,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Width = 10, Height = 10, Stretch = Stretch.Uniform, IsHitTestVisible = false,
            },
        };
        Glass.Paint(_pen);
        _pen.MouseLeftButtonDown += (_, e) => { e.Handled = true; _downPoint = null; _line.Edit(Item.Id); };
        _cardHost.Children.Add(_pen);

        // "Copied", under the card.
        var copiedRow = new StackPanel { Orientation = Orientation.Horizontal };
        copiedRow.Children.Add(Glass.Check(10, Theme.Freeze(new SolidColorBrush(Theme.Primary))));
        _copiedText = new TextBlock
        {
            Text = Strings.Copied, Margin = new Thickness(6, 0, 0, 0),
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI"), FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Freeze(new SolidColorBrush(Theme.Primary)), VerticalAlignment = VerticalAlignment.Center,
        };
        copiedRow.Children.Add(_copiedText);
        _copied = new Border
        {
            Child = copiedRow, Padding = new Thickness(10, 5, 10, 5), CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(0.75), Opacity = 0, IsHitTestVisible = false,
            RenderTransform = _copiedTranslate,
        };
        Glass.Paint(_copied);

        Children.Add(_cardHost);
        Children.Add(_copied);
        PaintPin();

        // A quiet tooltip: the file, its size and how long it has been hanging.
        var tip = new StackPanel();
        _tipName.FontWeight = FontWeights.SemiBold;
        _tipMeta.Opacity = 0.7;
        _tipMeta.Margin = new Thickness(0, 2, 0, 0);
        tip.Children.Add(_tipName);
        tip.Children.Add(_tipMeta);
        var toolTip = new ToolTip { Content = tip, FlowDirection = Strings.Flow };
        toolTip.Opened += (_, _) => RefreshTip();
        ToolTipService.SetInitialShowDelay(_cardHost, 900);
        ToolTipService.SetShowDuration(_cardHost, 6000);
        ToolTipService.SetPlacement(_cardHost, System.Windows.Controls.Primitives.PlacementMode.Bottom);
        _cardHost.ToolTip = toolTip;
        SetLeft(_cardHost, (Layout.CardWidth - _cardSize.Width) / 2);
        SetTop(_cardHost, Layout.CardOffsetBelowTop);
        PlaceCopied();

        _holdTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = HoldDuration };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            if (_downPoint is null || _startedDrag) return;
            _didLongPress = true;
            SetPressed(false);
            _line.Edit(Item.Id);
        };

        _cardHost.MouseLeftButtonDown += OnMouseDown;
        _cardHost.MouseMove += OnMouseMove;
        _cardHost.MouseLeftButtonUp += OnMouseUp;
        _cardHost.MouseRightButtonUp += OnRightClick;
        _cardHost.LostMouseCapture += (_, _) =>
        {
            if (!_startedDrag) EndPress();
            if (_reordering) { _reordering = false; (Parent as LineCanvas)?.EndReorder(this); }
        };

        Opacity = 0;
        Arrive();
    }

    private void ApplyPhotoClip()
    {
        var photo = Layout.PhotoSize(Item.PixelWidth, Item.PixelHeight);
        double r = Math.Max(0,Layout.FrameRadius - Layout.FrameInset);
        if (Notes.IsNote(Item.Path)) { _image.Clip = null; return; }
        _image.Clip = new RectangleGeometry(new Rect(0, 0, photo.Width, photo.Height), r, r);
    }

    private void PlaceCopied()
    {
        _copied.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var s = _copied.DesiredSize;
        SetLeft(_copied, (Layout.CardWidth - s.Width) / 2);
        SetTop(_copied, Layout.CardOffsetBelowTop + _cardSize.Height + 16 - s.Height / 2);
    }

    /// <summary>The thumbnail changed, for example after an edit.</summary>
    public void Refresh()
    {
        _image.Source = Item.Thumb;
        PaintPin();
        var size = Layout.CardSize(Item.PixelWidth, Item.PixelHeight);
        if (size != _cardSize)
        {
            _cardSize = size;
            _cardHost.Width = size.Width; _cardHost.Height = size.Height;
            _cardScale.CenterX = size.Width / 2;
            SetLeft(_cardHost, (Layout.CardWidth - size.Width) / 2);
            PlaceCopied();
        }
        ApplyPhotoClip();
        if (!Item.Flying) Nudge(2.2);
    }

    /// <summary>A stable number for this photo, so a mixed peg keeps its colour.</summary>
    private int Seed => Item.Path.Aggregate(17, (h, c) => h * 31 + c);

    private void PaintPin()
    {
        if (_pin is not null) Children.Remove(_pin);
        _pin = Pegs.Create(seed: Seed, pinned: Item.Pinned);
        Children.Add(_pin);
        SetZIndex(_pin, 3);
        SetLeft(_pin, (Layout.CardWidth - Layout.PinWidth) / 2);
        SetTop(_pin, 0);
    }

    private void RefreshTip()
    {
        _tipName.Text = System.IO.Path.GetFileName(Item.Path);
        _tipMeta.Text = $"{Item.PixelWidth} × {Item.PixelHeight}  ·  {Age(Item.HungAt)}{(Item.Pinned ? "  ·  " + Strings.KeepOnLine : "")}";
    }

    private static string Age(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalSeconds < 45) return Strings.JustNow;
        if (span.TotalMinutes < 60) return string.Format(Strings.MinutesAgo, (int)span.TotalMinutes);
        if (span.TotalHours < 24) return string.Format(Strings.HoursAgo, (int)span.TotalHours);
        return when.ToString("d MMM HH:mm");
    }

    // MARK: Layout on the line

    public void SetTargetX(double centerX, bool snap)
    {
        if (snap) _x.Snap(centerX); else _x.Target = centerX;
    }

    internal void FollowDrag(double centerX, double pullY, double velocity)
    {
        _dragVelocity=velocity;
        _x.Snap(centerX);
        _pull.Target = Math.Clamp(pullY*.45,-18,38);
        _swing.Target = Math.Clamp(-velocity*.003,-8,8)*MotionFeel.Amount;
        _hoverScale.Target = 1.045;
        Tick(0); // The held card reaches the pointer in the input event, before the next frame.
    }

    internal void ReleaseDrag()
    {
        _pull.Target=0;_swing.Target=0;
        _x.Velocity=Math.Clamp(_dragVelocity*.045,-65,65)*MotionFeel.Amount;
        _swing.Velocity+=Math.Clamp(_dragVelocity*.01,-12,12)*MotionFeel.Amount;
        _hoverScale.Target=Hovering?1.035:1;
    }

    public double CenterX => _x.Value;
    public double TargetX => _x.Target;
    /// <summary>The rope sags, so the card follows it up and down as it moves along.</summary>
    public Func<double, double>? RopeYAt { get; set; }

    /// <summary>The card's rectangle in canvas coordinates, ignoring the tilt. Used to decide where the pointer counts.</summary>
    public Rect CardRect
    {
        get
        {
            double top = GetTop(this);
            double scale = _cardScale.ScaleX;
            double w = _cardSize.Width * scale, h = _cardSize.Height * scale;
            double x = _x.Value - w / 2;
            double y = top + _position.Y + Layout.CardOffsetBelowTop + _drop.Y;
            return new Rect(x, y, w, h);
        }
    }

    // MARK: Motion

    private void Arrive()
    {
        if (Item.Flying)
        {
            // A capture that flew in is already in place; the flight did the arriving.
            _arrive.Snap(0);
            _swing.Snap(0);
            _opacity.Snap(0);
            return;
        }
        _swing.Snap(2+5*MotionFeel.Amount);
        _swing.Target = 0;
        _arrive.Target = 0;
        _opacity.Go(1, 0.28, Ease.OutQuad);
    }

    /// <summary>Landing after the flight: no jump, just a small sway from rest.</summary>
    public void Land()
    {
        _opacity.Go(1, 0.16, Ease.OutQuad);
        Nudge(2.2);
    }

    public void Breeze(bool now = false)
    {
        var rng = Random.Shared;
        double delay = rng.NextDouble() * 0.35;
        double degrees = 1.6 + rng.NextDouble() * 1.8;
        if (now) { Nudge(degrees); return; }
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.01, delay)) };
        t.Tick += (_, _) => { t.Stop(); Nudge(degrees); };
        t.Start();
    }

    private void Nudge(double degrees)
    {
        // A push, then the spring brings it back and lets it swing.
        _swing.Velocity += degrees * (2+6*MotionFeel.Amount);
        _swing.Target = 0;
    }

    public void SetHovering(bool on)
    {
        if (_hovering == on) return;
        _hovering = on;
        bool dragging = _line.DraggingId == Item.Id;
        _hoverScale.Target = on ? 1.035 : 1;
        _shadow.Go(on ? 1 : 0, 0.18);
        _crossOpacity.Go(on && !dragging ? 1 : 0, 0.18);
        _crossScale.Go(on ? 1 : 0.6, 0.18);
    }

    public void StateChanged()
    {
        bool copied = _line.CopiedId == Item.Id;
        if (copied) _copiedText.Text = _line.CopiedLabel;
        bool dragging = _line.DraggingId == Item.Id;
        if (copied && _copiedOpacity.Target < 1) { _copiedOpacity.Go(1, 0.2); _copiedOffset.Go(0, 0.2); Nudge(3); }
        if (!copied && _copiedOpacity.Target > 0) { _copiedOpacity.Go(0, 0.2); _copiedOffset.Go(-4, 0.2); }
        _cardOpacity.Go(dragging ? 0.45 : 1, 0.15);
        _crossOpacity.Go(_hovering && !dragging ? 1 : 0, 0.18);
    }

    private void SetPressed(bool on)
    {
        _line.PressedId = on ? Item.Id : (_line.PressedId == Item.Id ? null : _line.PressedId);
        // Holding presses the photo in slowly, so a long press feels like it is building up to something.
        if (on) _pressScale.Go(0.95, 0.45, Ease.InOutSine);
        else _pressScale.Go(1, 0.3, Ease.OutCubic);
    }

    /// <summary>Advances every spring and tween by `dt` seconds and applies them.</summary>
    public void Tick(double dt)
    {
        _swing.Step(dt); _arrive.Step(dt); _hoverScale.Step(dt); _pressScale.Step(dt); _x.Step(dt); _pull.Step(dt);
        _opacity.Step(dt); _cardOpacity.Step(dt); _crossOpacity.Step(dt); _crossScale.Step(dt);
        _copiedOpacity.Step(dt); _copiedOffset.Step(dt); _shadow.Step(dt);

        _position.X = _x.Value - Layout.CardWidth / 2;
        if (RopeYAt is not null)
        {
            double ropeY = RopeYAt(_x.Value);
            _position.Y = ropeY - Layout.PinAbove - GetTop(this);
        }
        _rotate.Angle = _swing.Value + Item.Tilt;
        _drop.Y = _arrive.Value + _pull.Value;
        Opacity = Item.Falling || Item.Flying ? 0 : _opacity.Value;

        double scale = _hoverScale.Value * _pressScale.Value;
        _cardScale.ScaleX = _cardScale.ScaleY = scale;
        _cardHost.Opacity = _cardOpacity.Value;

        double s = _shadow.Value;
        _shadowEffect.Opacity = Surfaces.Shadow + 0.06 * s;
        _shadowEffect.BlurRadius = 12;
        _shadowEffect.ShadowDepth = 3 + 2 * s;

        _cross.Opacity = _pen.Opacity = _crossOpacity.Value;
        ((ScaleTransform)_cross.RenderTransform).ScaleX = ((ScaleTransform)_cross.RenderTransform).ScaleY = _crossScale.Value;
        ((ScaleTransform)_pen.RenderTransform).ScaleX = ((ScaleTransform)_pen.RenderTransform).ScaleY = _crossScale.Value;
        _copied.Opacity = _copiedOpacity.Value;
        _copiedTranslate.Y = _copiedOffset.Value;
    }

    // MARK: Input

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            _downPoint = null;
            EndPress();
            if (_line.IsNote(Item.Id)) _line.EditNote(Item.Id); else _line.Open(Item.Id);
            return;
        }
        _downPoint = e.GetPosition(this);
        _downInCanvas = Parent is UIElement parent ? e.GetPosition(parent) : _downPoint.Value;
        _grabOffsetX = _downInCanvas.X - CenterX;
        _lastDragX=_downInCanvas.X;_dragTime=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;_dragVelocity=0;
        _startedDrag = false;
        _gestureMoved = false;
        _reordering = false;
        _didLongPress = false;
        _cardHost.CaptureMouse();
        SetPressed(true);
        _holdTimer.Stop();
        _holdTimer.Start();
    }

    private void EndPress()
    {
        _holdTimer.Stop();
        SetPressed(false);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_downPoint is null || _startedDrag || _didLongPress || e.LeftButton != MouseButtonState.Pressed) return;
        var canvas = Parent as LineCanvas;
        var p = canvas is not null ? e.GetPosition(canvas) : e.GetPosition(this);
        double dx = p.X - _downInCanvas.X, dy = p.Y - _downInCanvas.Y;
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        double elapsed=now-_dragTime;
        if(elapsed>0.001){_dragVelocity=Math.Clamp((p.X-_lastDragX)/elapsed,-2200,2200);_dragTime=now;_lastDragX=p.X;}

        if (_reordering)
        {
            // Pulled away from the line: it becomes a real drag into another app.
            if (Math.Abs(dy)>MotionFeel.PullThreshold(_cardSize.Height))
            {
                _reordering = false;
                canvas?.EndReorder(this);
                _startedDrag = true;
                _cardHost.ReleaseMouseCapture();
                StartDrag(e.GetPosition(_cardHost));
                return;
            }
            canvas?.ReorderTo(this, p.X - _grabOffsetX,dy,_dragVelocity);
            return;
        }

        if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;
        _gestureMoved=true;
        EndPress();
        if (canvas is not null && Math.Abs(dx)>=Math.Abs(dy)*.75 && !MotionFeel.PullsOut(dx,dy,_cardSize.Height))
        {
            // Sliding along the line reorders the photos.
            _reordering = true;
            canvas.BeginReorder(this);
            canvas.ReorderTo(this, p.X - _grabOffsetX,dy,_dragVelocity);
            return;
        }
        if (!MotionFeel.PullsOut(dx,dy,_cardSize.Height)) return;
        _startedDrag = true;
        _cardHost.ReleaseMouseCapture();
        StartDrag(e.GetPosition(_cardHost));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        bool click = _downPoint is not null && !_gestureMoved && !_startedDrag && !_didLongPress && !_reordering;
        EndPress();
        _cardHost.ReleaseMouseCapture();
        if (_reordering) { _reordering = false; (Parent as LineCanvas)?.EndReorder(this); }
        if (click) _line.Copy(Item.Id);
        _downPoint = null;
        _didLongPress = false;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        EndPress();
        _downPoint = null;
        Menus.Show(BuildMenu());
    }

    private ContextMenu BuildMenu()
    {
        var id = Item.Id;
        var menu = new ContextMenu { FlowDirection = Strings.Flow };
        menu.Opened += (_, _) => _line.MenuOpen = true;
        menu.Closed += (_, _) => _line.MenuOpen = false;
        bool inInbox = _line.IsInInbox(id);
        bool isNote = _line.IsNote(id);
        menu.Items.Add(MenuItemFor(Strings.Copy, () => _line.Copy(id)));
        if (isNote)
        {
            menu.Items.Add(MenuItemFor(Strings.EditNote, () => _line.EditNote(id)));
            var colours = new MenuItem { Header = Strings.NoteColour };
            foreach (var key in Notes.Colors)
            {
                var k = key;
                var swatch = new MenuItem { Header = Strings.NoteName(k), Icon = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Notes.Paper(k)) } };
                swatch.Click += (_, _) => _line.RecolourNote(id, k);
                colours.Items.Add(swatch);
            }
            menu.Items.Add(colours);
        }
        else
        {
            menu.Items.Add(MenuItemFor(Strings.Open, () => _line.Open(id)));
            menu.Items.Add(MenuItemFor(Strings.MarkUp, () => _line.Edit(id)));
            menu.Items.Add(MenuItemFor(Strings.EditInPaint, () => _line.EditExternal(id)));
            if (Ocr.Available) menu.Items.Add(MenuItemFor(Strings.CopyText, () => _line.CopyText(id)));
        }
        menu.Items.Add(MenuItemFor(Strings.ShowInExplorer, () => _line.Reveal(id)));
        var keep = MenuItemFor(Strings.KeepOnLine, () => _line.TogglePin(id));
        keep.IsChecked = Item.Pinned;
        menu.Items.Add(keep);
        if (inInbox)
        {
            menu.Items.Add(MenuItemFor(Strings.SaveToDesktop, () => _line.SaveToDesktop(id)));
            menu.Items.Add(MenuItemFor(Strings.SaveToScreenshots, () => _line.SaveToScreenshots(id)));
        }
        menu.Items.Add(new Separator());
        if (inInbox)
        {
            menu.Items.Add(MenuItemFor(Strings.TakeDown, () => _line.Discard(id)));
            menu.Items.Add(MenuItemFor(Strings.MoveToRecycleBin, () => _line.Trash(id)));
        }
        else
        {
            menu.Items.Add(MenuItemFor(Strings.TakeDown, () => _line.Discard(id)));
            menu.Items.Add(MenuItemFor(Strings.MoveToRecycleBin, () => _line.Trash(id)));
        }
        return menu;
    }

    public static MenuItem MenuItemFor(string header, Action action, string gesture = "")
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture };
        item.Click += (_, _) => action();
        return item;
    }

    // MARK: Drag and drop

    /// <summary>
    /// Drags the file out as Explorer would. An app gets a copy, and the photo
    /// stays. A folder keeps it, and the photo leaves. The Recycle Bin discards
    /// it. Nowhere that accepts it: nothing happens.
    /// </summary>
    private void StartDrag(Point grabInCard)
    {
        var source = PresentationSource.FromVisual(this);
        double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var image = RenderDragImage(scale);
        var grab = new Point(grabInCard.X * scale, grabInCard.Y * scale);

        _line.DraggingId = Item.Id;
        uint effect = DragSource.DROPEFFECT_NONE;
        try
        {
            effect = DragSource.Drag(Item.Path, image, grab);
        }
        catch (Exception ex)
        {
            Log.Error($"Drag failed: {ex.Message}");
        }
        finally
        {
            _line.DraggingId = null;
            _startedDrag = false;
            _downPoint = null;
        }
        Log.Notice($"Drag ended with effect {effect}");
        bool accepted = effect != DragSource.DROPEFFECT_NONE;
        if (accepted) Item.Used = true;

        // A target that reports a move is trusted to have done it: Explorer
        // moves the file itself, and the line notices it is gone. WinTendedero
        // never deletes a file on a drop target's word, so an app that
        // answers "move" after merely reading the file costs nothing.

        // Dropped into an app and sent on its way: with the option on, it leaves the line too.
        if (accepted && Settings.Current.TakeDownAfterDrag && System.IO.File.Exists(Item.Path))
        {
            var id = Item.Id;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.35) };
            t.Tick += (_, _) => { t.Stop(); if (_line.Find(id) is { Falling: false }) _line.Discard(id); };
            t.Start();
            return;
        }

        // Moved into a folder: it is saved where you wanted it, and leaves the
        // line. Explorer finishes a moment later, so look again then.
        _line.Prune();
        foreach (var delay in new[] { 0.3, 0.8, 2.0 })
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            t.Tick += (_, _) => { t.Stop(); _line.Prune(); };
            t.Start();
        }
    }

    /// <summary>The drag preview: the card as it hangs, without the clip.</summary>
    private BitmapSource RenderDragImage(double scale)
    {
        double w = _cardSize.Width, h = _cardSize.Height;
        double pad = 12;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(pad, pad));
            var frame = new Rect(0, 0, w, h);
            PhotoFrames.Draw(dc,new Size(w,h));
            var photo = PhotoFrames.PhotoRect(new Size(w,h));
            double r = Math.Max(0,Layout.FrameRadius - Layout.FrameInset);
            dc.PushClip(new RectangleGeometry(photo, r, r));
            dc.DrawImage(Item.Thumb, photo);
            dc.Pop();
            dc.DrawRoundedRectangle(null, new Pen(new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90), 0.75), frame, Layout.FrameRadius, Layout.FrameRadius);
            dc.Pop();
        }
        int pw = (int)Math.Ceiling((w + 2 * pad) * scale), ph = (int)Math.Ceiling((h + 2 * pad) * scale);
        var rtb = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        // The grab point is relative to the card, so shift it by the padding.
        return new CroppedBitmap(rtb, new Int32Rect((int)(pad * scale), (int)(pad * scale), (int)(w * scale), (int)(h * scale)));
    }
}
