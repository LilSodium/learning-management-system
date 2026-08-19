using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace CLI.LMS.MAUI;

// Draws one comment. Used by both the student and teacher pages so the
// thread looks the same either way.
public static class CommentViews
{
    public static View Build(AssignmentComment comment)
    {
        var header = new HorizontalStackLayout { Spacing = 8 };

        var authorLabel = new Label { Text = comment.Author, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        if (comment.IsInstructor) authorLabel.TextColor = Colors.MediumPurple;
        header.Children.Add(authorLabel);

        if (comment.IsInstructor)
        {
            header.Children.Add(new Label
            {
                Text = "Instructor",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.MediumPurple,
                VerticalOptions = LayoutOptions.Center
            });
        }

        header.Children.Add(new Label
        {
            Text = comment.PostedAt.ToString("g"),
            FontSize = 11,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        });

        var stack = new VerticalStackLayout { Spacing = 2 };
        stack.Children.Add(header);
        stack.Children.Add(new Label { Text = comment.Text });

        return new Border
        {
            Stroke = comment.IsInstructor ? Colors.MediumPurple : Colors.LightGray,
            StrokeThickness = 1,
            Padding = 10,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = stack
        };
    }
}
