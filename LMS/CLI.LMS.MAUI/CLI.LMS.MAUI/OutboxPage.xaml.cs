using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

public partial class OutboxPage : ContentPage
{
    private readonly IEmailService _emailService;

    public OutboxPage(IEmailService emailService)
    {
        InitializeComponent();
        _emailService = emailService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EmailList.ItemsSource = await _emailService.GetOutboxAsync();
    }
}
