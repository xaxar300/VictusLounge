using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;
using VictusLounge.Services;
using VictusLounge.ViewModels;

namespace VictusLounge;

public partial class MainWindow
{
    private static readonly string[] ThemeColorKeys =
    [
        "BgColor", "ShellColor", "PanelColor", "SurfaceColor", "TextColor", "MutedColor", "DimColor",
        "GoldColor", "GoldLightColor", "GoldDarkColor", "LineColor", "LineSoftColor", "OkColor",
        "DangerColor", "WaitColor"
    ];

    private static readonly (string BrushKey, string ColorKey)[] ThemeBrushes =
    [
        ("ShellBrush", "ShellColor"),
        ("PanelBrush", "PanelColor"),
        ("SurfaceBrush", "SurfaceColor"),
        ("TextBrush", "TextColor"),
        ("MutedBrush", "MutedColor"),
        ("DimBrush", "DimColor"),
        ("GoldBrush", "GoldColor"),
        ("GoldLightBrush", "GoldLightColor"),
        ("LineBrush", "LineColor"),
        ("LineSoftBrush", "LineSoftColor"),
        ("OkBrush", "OkColor"),
        ("DangerBrush", "DangerColor"),
        ("WaitBrush", "WaitColor")
    ];

    private static readonly (string BrushKey, string[] ColorKeys)[] ThemeGradients =
    [
        ("WindowBackgroundBrush", ["BgColor", "ShellColor", "BgColor"]),
        ("ShellBackgroundBrush", ["ShellColor", "BgColor"]),
        ("TitlebarBrush", ["ShellColor", "SurfaceColor"]),
        ("GoldButtonBrush", ["GoldLightColor", "GoldDarkColor"]),
        ("PanelGradientBrush", ["PanelColor", "SurfaceColor"])
    ];

    private void SelectAuthRole(string role)
    {
        _currentRole = role;
        UpdateAuthRoleButtons();
        ShowStatus("Role selected", $"After login: {GetRoleTitle(_currentRole)} mode.");
    }

    private void ShowAuthView(bool isRegister)
    {
        _viewModel.Auth.IsRegisterMode = isRegister;
        ApplyAuthViewState();
    }

    private void ApplyAuthViewState()
    {
        var isRegister = _viewModel.Auth.IsRegisterMode;
        LoginAuthView.Visibility = isRegister ? Visibility.Collapsed : Visibility.Visible;
        RegisterAuthView.Visibility = isRegister ? Visibility.Visible : Visibility.Collapsed;
        AuthErrorText.Text = _viewModel.Auth.ErrorMessage;
        RegisterErrorText.Text = _viewModel.Auth.RegisterErrorMessage;
        AuthErrorText.Visibility = _viewModel.Auth.HasError ? Visibility.Visible : Visibility.Collapsed;
        RegisterErrorText.Visibility = _viewModel.Auth.HasRegisterError ? Visibility.Visible : Visibility.Collapsed;
        AuthWindowTitleText.Text = _viewModel.Auth.AuthTitle;
        UpdateAuthRoleButtons();
    }

    private void SignInUser(User user)
    {
        _currentUserId = user.Id;
        _currentUserFullName = user.FullName;
        _currentUserLogin = user.Login;
        _currentRole = NormalizeRole(user.Role);
        _balanceAmount = user.Balance;

        AuthErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        _viewModel.Navigation.IsAuthOverlayVisible = false;

        LoadDatabaseState();
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        NavigateTo(GetDefaultViewForRole(_currentRole));
        ApplySidebarState(false);
        ShowStatus("Вход выполнен", $"{_currentUserFullName}: открыт режим {GetRoleTitle(_currentRole)}.");
    }

    private void ShowAuthError(string message)
    {
        _viewModel.Auth.ErrorMessage = message;
        ApplyAuthViewState();
    }

    private void ShowRegisterError(string message)
    {
        _viewModel.Auth.RegisterErrorMessage = message;
        ApplyAuthViewState();
    }

    private void UpdateCurrentUserUi()
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateCurrentBalanceText();
        SyncCurrentUserViewModel();
    }

    private static string GetClientTier(double playedHours)
    {
        return LoyaltyTierService.GetTier(playedHours);
    }

    private static string GetClientTier(User user)
    {
        return string.IsNullOrWhiteSpace(user.LoyaltyTier) ? "Bronze" : user.LoyaltyTier;
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "admin" => "admin",
            "owner" => "owner",
            _ => "client"
        };
    }

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "??";
        }

        var initials = string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        return initials.Length == 1 ? $"{initials}." : initials;
    }

    private void UpdateAuthRoleButtons()
    {
        if (!IsLoaded)
        {
            return;
        }

        SetChoiceButtonStyles(_currentRole,
            ("client", LoginRoleClientButton),
            ("admin", LoginRoleAdminButton),
            ("owner", LoginRoleOwnerButton),
            ("client", RegisterRoleClientButton),
            ("admin", RegisterRoleAdminButton),
            ("owner", RegisterRoleOwnerButton));
    }

    private void ApplyRoleAccess(bool navigateIfNeeded = true)
    {
        if (navigateIfNeeded && !IsViewAllowedForRole(_currentView))
        {
            NavigateTo(GetDefaultViewForRole(_currentRole));
        }

        ApplySidebarState(false);
    }

    private bool IsViewAllowedForRole(string view)
    {
        return _currentRole switch
        {
            "client" => view is "dashboard" or "map" or "booking" or "cabinet" or "balance" or "events" or "settings",
            "admin" => view is "map" or "admin" or "shift" or "settings",
            "owner" => view is "owner" or "admin" or "shift" or "settings",
            _ => view is "settings"
        };
    }

    private static string GetDefaultViewForRole(string role)
    {
        return role switch
        {
            "admin" => "admin",
            "owner" => "owner",
            _ => "dashboard"
        };
    }

    private static string GetRoleTitle(string role)
    {
        return role switch
        {
            "admin" => "Admin",
            "owner" => "Owner",
            _ => "Client"
        };
    }

    private void ConfigureNavigationCommands()
    {
        _viewModel.Navigation.NavigateCommand = new RelayCommand(view => NavigateTo(view?.ToString() ?? "dashboard"));
        _viewModel.Navigation.LogoutCommand = new RelayCommand(Logout);
        _viewModel.Navigation.ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
    }

    private void Logout()
    {
        _currentUserId = 0;
        _currentUserFullName = "Not signed in";
        _currentUserLogin = string.Empty;
        _currentRole = "client";
        _viewModel.Navigation.IsAuthOverlayVisible = true;
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        ApplyRoleAccess(false);
        ShowAuthView(isRegister: false);
    }

    private void ToggleSidebar()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        _viewModel.Navigation.IsSidebarCollapsed = _isSidebarCollapsed;

        ApplySidebarState();

        ShowStatus(
            _isSidebarCollapsed ? "Меню свернуто" : "Меню раскрыто",
            _isSidebarCollapsed ? "Навигация оставлена в компактном режиме." : "Полная навигация снова доступна.");
    }

    private void ApplySidebarState(bool focusToggle = true)
    {
        SidebarColumn.Width = new GridLength(_isSidebarCollapsed ? 112 : 264);
        Sidebar.Padding = _isSidebarCollapsed ? new Thickness(10, 19, 10, 19) : new Thickness(19);

        BrandTitle.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BrandSubtitle.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BrandTextPanel.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarProfileCard.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ProfileText.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        LogoutText.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarVersionText.HorizontalAlignment = _isSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        SidebarToggleIcon.Data = Geometry.Parse(_isSidebarCollapsed
            ? "M 5 5 L 9 9 L 5 13"
            : "M 9 5 L 5 9 L 9 13");

        foreach (var button in new[]
        {
            DashboardNavButton,
            MapNavButton,
            BookingNavButton,
            CabinetNavButton,
            BalanceNavButton,
            EventsNavButton,
            AdminNavButton,
            ShiftNavButton,
            OwnerNavButton,
            SettingsNavButton
        })
        {
            ApplySidebarButtonState(button);
        }

        if (focusToggle)
        {
            SidebarToggle.Focus();
        }
    }

    private void ApplySidebarButtonState(Button button)
    {
        if (button.Content is not StackPanel stack)
        {
            return;
        }

        stack.HorizontalAlignment = _isSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        var textBlocks = stack.Children.OfType<TextBlock>().ToArray();
        if (textBlocks.Length == 0)
        {
            return;
        }

        textBlocks[0].Width = _isSidebarCollapsed ? 44 : 30;
        textBlocks[0].TextAlignment = TextAlignment.Center;

        foreach (var label in textBlocks.Skip(1))
        {
            label.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void NavigateTo(string view)
    {
        if (!IsViewAllowedForRole(view))
        {
            ShowStatus("Раздел недоступен", $"{GetRoleTitle(_currentRole)} работает только со своими рабочими инструментами.");
            return;
        }

        _currentView = view;
        _viewModel.Navigation.CurrentView = view;
        DashboardView.Visibility = view == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        MapView.Visibility = view == "map" ? Visibility.Visible : Visibility.Collapsed;
        BookingView.Visibility = view == "booking" ? Visibility.Visible : Visibility.Collapsed;
        CabinetView.Visibility = view == "cabinet" ? Visibility.Visible : Visibility.Collapsed;
        BalanceView.Visibility = view == "balance" ? Visibility.Visible : Visibility.Collapsed;
        EventsView.Visibility = view == "events" ? Visibility.Visible : Visibility.Collapsed;
        AdminView.Visibility = view == "admin" ? Visibility.Visible : Visibility.Collapsed;
        ShiftView.Visibility = view == "shift" ? Visibility.Visible : Visibility.Collapsed;
        OwnerView.Visibility = view == "owner" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = view == "settings" ? Visibility.Visible : Visibility.Collapsed;

        var title = view switch
        {
            "map" => T("Nav.Map"),
            "booking" => T("Nav.Booking"),
            "cabinet" => T("Nav.Cabinet"),
            "balance" => T("Nav.Balance"),
            "events" => T("Nav.Events"),
            "admin" => T("Nav.Admin"),
            "shift" => T("Nav.Shift"),
            "owner" => T("Nav.Owner"),
            "settings" => T("Nav.Settings"),
            _ => T("Nav.Dashboard")
        };
        _viewModel.Navigation.CurrentTitle = title;
        if (view is "map" or "cabinet" or "balance")
        {
            LoadDatabaseState();
        }
        if (view == "map")
        {
            Dispatcher.InvokeAsync(ApplyMapPcButtonStatuses, DispatcherPriority.Loaded);
        }
        ShowStatus(title, $"Открыт раздел: {title}.");
    }

    private void ApplyTheme(string theme, bool showToast = true)
    {
        _currentTheme = theme;
        ApplyThemeResources(theme);
        UpdateThemeButtons();
        SaveUserSettings();
        if (showToast)
        {
            ShowStatus(T("Settings.Applied"), T("Settings.ThemeApplied"));
        }
    }

    private void ApplyThemeResources(string theme)
    {
        var themeDictionary = LoadDictionary($"Resources/Themes.{theme}.xaml");

        foreach (var colorKey in ThemeColorKeys)
        {
            SetThemeColor(themeDictionary, colorKey);
        }

        foreach (var (brushKey, colorKey) in ThemeBrushes)
        {
            SetBrushColor(brushKey, colorKey);
        }

        foreach (var (brushKey, colorKeys) in ThemeGradients)
        {
            SetGradient(brushKey, colorKeys);
        }
    }

    private void UpdateThemeButtons()
    {
        SetChoiceButtonStyles(_currentTheme,
            ("BlackGold", BlackGoldThemeButton),
            ("Graphite", GraphiteThemeButton),
            ("Light", LightThemeButton));
    }

    private void ApplyInterfaceSize(string size, bool showToast = true)
    {
        _currentInterfaceSize = size;

        var scale = size switch
        {
            "compact" => 0.92,
            "large" => 1.08,
            _ => 1.0
        };

        ShellRoot.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        UpdateInterfaceSizeButtons();
        SaveUserSettings();

        if (showToast)
        {
            ShowStatus(T("Settings.Applied"), T("Settings.InterfaceSizeApplied"));
        }
    }

    private void UpdateInterfaceSizeButtons()
    {
        SetChoiceButtonStyles(_currentInterfaceSize,
            ("compact", CompactInterfaceButton),
            ("normal", NormalInterfaceButton),
            ("large", LargeInterfaceButton));
    }

    private void ApplyLanguage(string language, bool showToast = true)
    {
        _currentLanguage = language;
        _languageStrings = LoadDictionary($"Resources/Strings.{language}.xaml");

        TopLanguageLabel.Text = T("Top.Language");
        GlobalSearchBox.ToolTip = T("Common.SearchPlaceholder");

        SetLocalizedText(
            (SettingsTitleText, "Settings.Title"),
            (SettingsSubtitleText, "Settings.Subtitle"),
            (SettingsThemeTitleText, "Settings.ThemeTitle"),
            (SettingsThemeDescriptionText, "Settings.ThemeDescription"),
            (SettingsLanguageTitleText, "Settings.LanguageTitle"),
            (SettingsLanguageDescriptionText, "Settings.LanguageDescription"),
            (SettingsInterfaceSizeTitleText, "Settings.InterfaceSizeTitle"),
            (SettingsInterfaceSizeDescriptionText, "Settings.InterfaceSizeDescription"));
        SetLocalizedContent(
            (BlackGoldThemeButton, "Settings.BlackGold"),
            (GraphiteThemeButton, "Settings.Graphite"),
            (LightThemeButton, "Settings.Light"),
            (CompactInterfaceButton, "Settings.InterfaceCompact"),
            (NormalInterfaceButton, "Settings.InterfaceNormal"),
            (LargeInterfaceButton, "Settings.InterfaceLarge"),
            (ConfirmActionsCheckBox, "Settings.ConfirmActions"));
        ApplyBalanceLanguage();

        SetNavButtonText(DashboardNavButton, T("Nav.Dashboard"));
        SetNavButtonText(MapNavButton, T("Nav.Map"));
        SetNavButtonText(BookingNavButton, T("Nav.Booking"));
        SetNavButtonText(CabinetNavButton, T("Nav.Cabinet"));
        SetNavButtonText(BalanceNavButton, T("Nav.Balance"));
        SetNavButtonText(EventsNavButton, T("Nav.Events"));
        SetNavButtonText(AdminNavButton, T("Nav.Admin"));
        SetNavButtonText(ShiftNavButton, T("Nav.Shift"));
        SetNavButtonText(OwnerNavButton, T("Nav.Owner"));
        SetNavButtonText(SettingsNavButton, T("Nav.Settings"));

        SetChoiceButtonStyles(language,
            ("ru", RuLanguageButton),
            ("en", EnLanguageButton),
            ("ru", TopRuButton),
            ("en", TopEnButton));
        _viewModel.Navigation.CurrentTitle = _currentView switch
        {
            "map" => T("Nav.Map"),
            "booking" => T("Nav.Booking"),
            "cabinet" => T("Nav.Cabinet"),
            "balance" => T("Nav.Balance"),
            "events" => T("Nav.Events"),
            "admin" => T("Nav.Admin"),
            "shift" => T("Nav.Shift"),
            "owner" => T("Nav.Owner"),
            "settings" => T("Nav.Settings"),
            _ => T("Nav.Dashboard")
        };
        LocalizeVisualTree(this);
        ApplyRoleAccess(false);
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        UpdateBookingSeatButtons();
        SaveUserSettings();

        if (showToast)
        {
            ShowStatus(T("Settings.Applied"), T("Settings.LanguageApplied"));
        }
    }

    private void SetActionConfirmation(bool confirm)
    {
        _confirmClientActions = confirm;
        SaveUserSettings();
    }

    private void SaveUserSettings()
    {
        _userSettings = new UserSettings(_currentTheme, _currentInterfaceSize, _currentLanguage, _confirmClientActions);
        _userSettingsStore.Save(_userSettings);
    }

    private static ResourceDictionary LoadDictionary(string path)
    {
        return (ResourceDictionary)Application.LoadComponent(new Uri(path, UriKind.Relative));
    }

    private string T(string key)
    {
        return _languageStrings.Contains(key) ? _languageStrings[key]?.ToString() ?? key : key;
    }

    private void SetLocalizedText(params (TextBlock Target, string Key)[] items)
    {
        foreach (var (target, key) in items)
        {
            target.Text = T(key);
        }
    }

    private void SetLocalizedContent(params (ContentControl Target, string Key)[] items)
    {
        foreach (var (target, key) in items)
        {
            target.Content = T(key);
        }
    }

    private void ApplyBalanceLanguage()
    {
        if (!IsLoaded)
        {
            return;
        }

        SetLocalizedText(
            (BalanceKickerText, "Balance.Kicker"),
            (BalanceTitleText, "Balance.Title"),
            (BalanceSubtitleText, "Balance.Subtitle"),
            (BalanceCurrentLabelText, "Balance.Current"),
            (BalancePromoLabelText, "Balance.Promo"));
        SetLocalizedContent(
            (BalanceTopupCardButton, "Balance.Topup"),
            (BalanceApplyPromoButton, "Balance.Apply"));
        if (_viewModel.Balance.IsRegularPackagesVisible)
        {
            _viewModel.Balance.ShowDefaultPackageOffer(T("Balance.Packages"), T("Balance.QuickGame"), T("Balance.Buy"));
        }
        SetLocalizedText(
            (EveningPackageText, "Balance.EveningPack"),
            (NightPackageText, "Balance.NightPack"),
            (BootcampPackageText, "Balance.BootcampTraining"),
            (WeekendPackageText, "Balance.WeekendPass"),
            (BalancePersonalOfferLabelText, "Balance.PersonalOffer"));
        SetLocalizedContent(
            (EveningBuyButton, "Balance.Buy"),
            (NightBuyButton, "Balance.Buy"),
            (BootcampBuyButton, "Balance.Buy"),
            (WeekendBuyButton, "Balance.Buy"),
            (BalanceOfferButton, "Balance.Activate"),
            (BalanceReceiptButton, "Balance.Export"));
        _viewModel.Balance.PersonalOfferText = T("Balance.PersonalOfferText");
        SetLocalizedText(
            (BalanceHistoryTitleText, "Balance.History"),
            (BalanceDateHeaderText, "Table.Date"),
            (BalanceOperationHeaderText, "Balance.Operation"),
            (BalanceMethodHeaderText, "Balance.Method"),
            (BalanceSumHeaderText, "Table.Amount"),
            (BalanceStatusHeaderText, "Balance.Status"));
        RefreshBalanceHistoryFromDatabase();
    }

    private string GetStatusText(string status, bool useMaintenanceWord = false)
    {
        return status switch
        {
            PcStatuses.Free => T("Status.Free"),
            PcStatuses.Busy => T("Status.Busy"),
            PcStatuses.Reserved => T("Status.Reserved"),
            PcStatuses.Service => T(useMaintenanceWord ? "Status.Maintenance" : "Status.Service"),
            _ => status
        };
    }

    private static void SetThemeColor(ResourceDictionary themeDictionary, string colorKey)
    {
        Application.Current.Resources[colorKey] = (Color)themeDictionary[$"Theme.{colorKey}"];
    }

    private static void SetBrushColor(string brushKey, string colorKey)
    {
        var color = (Color)Application.Current.Resources[colorKey];
        if (Application.Current.Resources[brushKey] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                Application.Current.Resources[brushKey] = new SolidColorBrush(color);
                return;
            }

            brush.Color = color;
            return;
        }

        Application.Current.Resources[brushKey] = new SolidColorBrush(color);
    }

    private static void SetGradient(string brushKey, params string[] colorKeys)
    {
        if (Application.Current.Resources[brushKey] is not LinearGradientBrush brush)
        {
            Application.Current.Resources[brushKey] = CreateGradient(colorKeys);
            return;
        }

        if (brush.IsFrozen)
        {
            Application.Current.Resources[brushKey] = CreateGradient(colorKeys);
            return;
        }

        for (var i = 0; i < brush.GradientStops.Count && i < colorKeys.Length; i++)
        {
            brush.GradientStops[i].Color = (Color)Application.Current.Resources[colorKeys[i]];
        }
    }

    private static LinearGradientBrush CreateGradient(params string[] colorKeys)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = colorKeys.Length > 2 ? new Point(1, 1) : new Point(0, 1)
        };

        for (var i = 0; i < colorKeys.Length; i++)
        {
            var offset = colorKeys.Length == 1 ? 0 : (double)i / (colorKeys.Length - 1);
            brush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources[colorKeys[i]], offset));
        }

        return brush;
    }

    private void SetChoiceButtonStyles(string activeKey, params (string Key, Button Button)[] choices)
    {
        foreach (var (key, button) in choices)
        {
            button.Style = (Style)FindResource(activeKey == key ? "PrimaryButtonStyle" : "GhostButtonStyle");
        }
    }

    private void SetTaggedChoiceButtonStyles(string activeTag, params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            var isActive = string.Equals(button.Tag?.ToString(), activeTag, StringComparison.OrdinalIgnoreCase);
            button.Style = (Style)FindResource(isActive ? "PrimaryButtonStyle" : "GhostButtonStyle");
        }
    }

    private static void SetNavButtonText(Button button, string text)
    {
        if (button.Content is not StackPanel stack)
        {
            return;
        }

        foreach (var child in stack.Children.OfType<TextBlock>().Skip(1).Take(1))
        {
            child.Text = text;
        }
    }

    private void LocalizeVisualTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            switch (child)
            {
                case TextBlock textBlock:
                    LocalizeTextBlock(textBlock);
                    break;
                case ContentControl { Content: string } contentControl:
                    LocalizeContentControl(contentControl);
                    break;
            }

            LocalizeVisualTree(child);
        }
    }

    private void LocalizeTextBlock(TextBlock textBlock)
    {
        var key = textBlock.GetValue(LocalizationKeyProperty) as string;
        if (key is null && LocalizedTextKeys.TryGetValue(textBlock.Text, out var detectedKey))
        {
            key = detectedKey;
            textBlock.SetValue(LocalizationKeyProperty, key);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            textBlock.Text = T(key);
        }
    }

    private void LocalizeContentControl(ContentControl contentControl)
    {
        var content = contentControl.Content as string;
        if (content is null)
        {
            return;
        }

        var key = contentControl.GetValue(LocalizationKeyProperty) as string;
        if (key is null && LocalizedTextKeys.TryGetValue(content, out var detectedKey))
        {
            key = detectedKey;
            contentControl.SetValue(LocalizationKeyProperty, key);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            contentControl.Content = T(key);
        }
    }

}
