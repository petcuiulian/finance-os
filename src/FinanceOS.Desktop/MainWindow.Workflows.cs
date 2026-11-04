using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FinanceOS.Core;
using FinanceOS.Infrastructure;

namespace FinanceOS.Desktop;

public partial class MainWindow
{
    private readonly SqliteWorkflowStore _workflows;
    private WorkTask? _workflowSelectedTask;
    private WorkflowCard? _selectedCard;
    private bool _loadingWorkflow;
    private readonly DispatcherTimer _workflowTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateOnly _lastRecurringDate;

    private void InitializeWorkflowLifecycle()
    {
        WaitingFollowupPicker.SelectedDate = BusinessDays.Add(DateOnly.FromDateTime(DateTime.Today), 2).ToDateTime(TimeOnly.MinValue);
        TaskRepeatDate.SelectedDate = DateTime.Today;
        _lastRecurringDate = DateOnly.FromDateTime(DateTime.Today);
        _workflowTimer.Tick += async (_, _) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today <= _lastRecurringDate) return;
            try
            {
                await _workflows.ReconcileRecurringAsync(today);
                _lastRecurringDate = today;
                await RefreshTasksAsync();
                await RefreshWorkflowsAsync();
            }
            catch (Exception) { WorkflowStatusText.Text = "Recurring tasks could not be generated. They will retry."; }
        };
        _workflowTimer.Start();
        Closed += (_, _) => _workflowTimer.Stop();
    }

    private async Task RefreshWorkflowsAsync()
    {
        if (!_ready) return;
        try
        {
            _loadingWorkflow = true;
            var selectedTemplate = (WorkflowTemplateBox.SelectedItem as WorkflowTemplate)?.Id;
            var templates = await _workflows.TemplatesAsync();
            WorkflowTemplateBox.ItemsSource = templates;
            WorkflowTemplateBox.SelectedItem = templates.FirstOrDefault(x => x.Id == selectedTemplate) ?? templates.FirstOrDefault();
            var statuses = await _workflows.StatusesAsync();
            CustomStatusBox.ItemsSource = statuses;
            var tasks = await _tasks.ListAsync(new TaskQuery("Tasks", Filter: "Open"), _lifetime.Token);
            DependencyTaskBox.ItemsSource = tasks;
            var waiting = await _workflows.WaitingAsync();
            WaitingGrid.ItemsSource = waiting;
            _loadingWorkflow = false;
            await RefreshStagesAndCardsAsync();
        }
        catch (Exception) { _loadingWorkflow = false; WorkflowStatusText.Text = "Workflows could not be loaded. Existing data was preserved."; }
    }

    private async Task RefreshStagesAndCardsAsync()
    {
        if (WorkflowTemplateBox.SelectedItem is not WorkflowTemplate template) return;
        try
        {
            var stageId = (WorkflowStageBox.SelectedItem as WorkflowStage)?.Id;
            var stages = await _workflows.StagesAsync(template.Id);
            WorkflowStageBox.ItemsSource = stages;
            WorkflowStageBox.SelectedItem = stages.FirstOrDefault(x => x.Id == stageId) ?? stages.FirstOrDefault();
            var cards = await _workflows.CardsAsync(template.Id);
            KanbanHost.Children.Clear();
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                var column = new StackPanel { Width = 205, Margin = new Thickness(0, 0, 10, 0) };
                column.Children.Add(new TextBlock { Text = stage.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 4, 8, 12) });
                foreach (var card in cards.Where(x => x.StageId == stage.Id))
                {
                    var row = new StackPanel { Margin = new Thickness(0, 0, 0, 7) };
                    var select = new Button { Content = card.Title + " · " + card.Status, Tag = card, HorizontalContentAlignment = HorizontalAlignment.Left };
                    select.Click += OnSelectCard;
                    row.Children.Add(select);
                    var actions = new StackPanel { Orientation = Orientation.Horizontal };
                    if (i > 0) actions.Children.Add(MoveCardButton("←", card, stages[i - 1]));
                    if (i + 1 < stages.Count) actions.Children.Add(MoveCardButton("→", card, stages[i + 1]));
                    row.Children.Add(actions); column.Children.Add(row);
                }
                var border = new Border
                {
                    Child = column,
                    Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10)
                };
                KanbanHost.Children.Add(border);
            }
            WorkflowStatusText.Text = $"{cards.Count} cards · {stages.Count} stages";
        }
        catch (Exception) { WorkflowStatusText.Text = "Kanban could not be loaded."; }
    }

    private Button MoveCardButton(string caption, WorkflowCard card, WorkflowStage next)
    {
        var button = new Button { Content = caption, Width = 42, Tag = (card, next), Margin = new Thickness(0, 5, 5, 0) };
        button.Click += async (_, _) =>
        {
            try
            {
                await _workflows.AssignTaskAsync(card.TaskId, next.TemplateId, next.Id);
                await ReconcileRemindersAsync();
                await RefreshStagesAndCardsAsync();
            }
            catch (Exception) { WorkflowStatusText.Text = "Card could not be moved."; }
        };
        return button;
    }

    private async void OnSelectCard(object sender, RoutedEventArgs e)
    {
        _selectedCard = (WorkflowCard)((Button)sender).Tag;
        KanbanSelectedText.Text = _selectedCard.Title + " · " + _selectedCard.StageName;
        var due = await _workflows.CardDeadlineAsync(_selectedCard.TaskId);
        if (due is not null) KanbanSelectedText.Text += " · Due " + due.Value.ToString("d");
        await RefreshChecklistAsync();
    }
    private async Task RefreshChecklistAsync()
    {
        StageChecklistList.Items.Clear();
        if (_selectedCard is null) return;
        try
        {
            foreach (var item in await _workflows.ChecklistAsync(_selectedCard.TaskId, _selectedCard.StageId))
            {
                var checkbox = new CheckBox { Content = item.Title, IsChecked = item.Done, Tag = item.Id, Margin = new Thickness(0, 3, 0, 3) };
                checkbox.Checked += OnChecklistChanged; checkbox.Unchecked += OnChecklistChanged;
                StageChecklistList.Items.Add(checkbox);
            }
        }
        catch (Exception) { WorkflowStatusText.Text = "Checklist could not be loaded."; }
    }
    private async void OnChecklistChanged(object sender, RoutedEventArgs e)
    {
        var item = (CheckBox)sender;
        try { await _workflows.SetChecklistAsync((string)item.Tag, item.IsChecked == true); }
        catch (Exception) { WorkflowStatusText.Text = "Checklist change failed."; await RefreshChecklistAsync(); }
    }
    private async void OnAddChecklist(object sender, RoutedEventArgs e)
    {
        if (_selectedCard is null) { WorkflowStatusText.Text = "Select a card first."; return; }
        try { await _workflows.AddChecklistAsync(_selectedCard.TaskId, _selectedCard.StageId, ChecklistTitleBox.Text); ChecklistTitleBox.Clear(); await RefreshChecklistAsync(); }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Checklist item could not be saved."; }
    }
    private async void OnWorkflowTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingWorkflow && _ready) await RefreshStagesAndCardsAsync();
    }
    private async void OnWorkflowStageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkflowStageBox.SelectedItem is not WorkflowStage stage) return;
        try
        {
            var (days, minutes) = await _workflows.StageDeadlineAsync(stage.Id);
            StageDueDaysBox.Text = days?.ToString() ?? "";
            StageReminderMinutesBox.Text = minutes?.ToString() ?? "";
        }
        catch (Exception) { WorkflowStatusText.Text = "Stage deadline could not be loaded."; }
    }
    private async void OnSaveStageDeadline(object sender, RoutedEventArgs e)
    {
        if (WorkflowStageBox.SelectedItem is not WorkflowStage stage) return;
        if (!int.TryParse(StageDueDaysBox.Text, out var days) ||
            (StageReminderMinutesBox.Text.Length > 0 && !int.TryParse(StageReminderMinutesBox.Text, out _)))
        { WorkflowStatusText.Text = "Enter a due-day offset and optional reminder minutes."; return; }
        try
        {
            var minutes = StageReminderMinutesBox.Text.Length == 0 ? (int?)null : int.Parse(StageReminderMinutesBox.Text);
            await _workflows.AddStageDeadlineAsync(stage.Id, days, minutes);
            WorkflowStatusText.Text = "Stage deadline saved. It applies when a card enters this stage.";
        }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Stage deadline could not be saved."; }
    }
    private async void OnCreateTemplate(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = await _workflows.CreateTemplateAsync(WorkflowNameBox.Text);
            WorkflowNameBox.Clear(); await RefreshWorkflowsAsync();
            if (WorkflowTemplateBox.ItemsSource is IEnumerable<WorkflowTemplate> templates) WorkflowTemplateBox.SelectedItem = templates.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Template could not be created. The name may already exist."; }
    }
    private async void OnRenameTemplate(object sender, RoutedEventArgs e)
    {
        if (WorkflowTemplateBox.SelectedItem is not WorkflowTemplate template) return;
        try { await _workflows.RenameTemplateAsync(template.Id, WorkflowNameBox.Text); await RefreshWorkflowsAsync(); }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Template could not be renamed."; }
    }
    private async void OnAddStage(object sender, RoutedEventArgs e)
    {
        if (WorkflowTemplateBox.SelectedItem is not WorkflowTemplate template) return;
        try { await _workflows.AddStageAsync(template.Id, WorkflowStageNameBox.Text); WorkflowStageNameBox.Clear(); await RefreshStagesAndCardsAsync(); }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Stage could not be added."; }
    }
    private async void OnRenameStage(object sender, RoutedEventArgs e)
    {
        if (WorkflowStageBox.SelectedItem is not WorkflowStage stage) return;
        try { await _workflows.RenameStageAsync(stage.Id, WorkflowStageNameBox.Text); await RefreshStagesAndCardsAsync(); }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Stage could not be renamed."; }
    }
    private async void OnMoveStageUp(object sender, RoutedEventArgs e) => await MoveStageAsync(-1);
    private async void OnMoveStageDown(object sender, RoutedEventArgs e) => await MoveStageAsync(1);
    private async Task MoveStageAsync(int delta)
    {
        if (WorkflowStageBox.SelectedItem is not WorkflowStage stage) return;
        try { await _workflows.MoveStageAsync(stage.Id, delta); await RefreshStagesAndCardsAsync(); }
        catch (Exception) { WorkflowStatusText.Text = "Stage could not be reordered."; }
    }
    private async void OnAssignWorkflow(object sender, RoutedEventArgs e)
    {
        if (_workflowSelectedTask is null || WorkflowTemplateBox.SelectedItem is not WorkflowTemplate template || WorkflowStageBox.SelectedItem is not WorkflowStage stage)
        { WorkflowStatusText.Text = "Select a task in Tasks, then choose a template and stage."; return; }
        try { await _workflows.AssignTaskAsync(_workflowSelectedTask.Id, template.Id, stage.Id); await ReconcileRemindersAsync(); await RefreshStagesAndCardsAsync(); }
        catch (Exception) { WorkflowStatusText.Text = "Task could not be assigned."; }
    }
    private async void OnAddCustomStatus(object sender, RoutedEventArgs e)
    {
        try { await _workflows.AddStatusAsync(NewStatusBox.Text); NewStatusBox.Clear(); await RefreshWorkflowsAsync(); }
        catch (Exception ex) { WorkflowStatusText.Text = ex is ArgumentException ? ex.Message : "Status could not be added."; }
    }
    private async void OnApplyCustomStatus(object sender, RoutedEventArgs e)
    {
        var id = _selectedCard?.TaskId ?? _workflowSelectedTask?.Id;
        if (id is null || CustomStatusBox.SelectedItem is not string status) { WorkflowStatusText.Text = "Select a task and custom status."; return; }
        try { await _workflows.SetCustomStatusAsync(id, status); await RefreshStagesAndCardsAsync(); }
        catch (Exception) { WorkflowStatusText.Text = "Status could not be applied."; }
    }
    private async void OnWaitingSelected(object sender, SelectionChangedEventArgs e)
    {
        if (WaitingGrid.SelectedItem is not WaitingTask item) return;
        WaitingContactBox.Text = item.Contact;
        WaitingExpectedPicker.SelectedDate = item.ExpectedDate?.ToDateTime(TimeOnly.MinValue);
        WaitingFollowupPicker.SelectedDate = item.FollowupDate.ToDateTime(TimeOnly.MinValue);
        WaitingDetailText.Text = $"Waiting {BusinessDays.Count(DateOnly.FromDateTime(item.StartedUtc.LocalDateTime), DateOnly.FromDateTime(DateTime.Today))} business days. " +
            (item.FollowupDate < DateOnly.FromDateTime(DateTime.Today) ? "Follow-up overdue; consider escalation." : "Follow-up scheduled.");
        WaitingHistoryList.ItemsSource = await _workflows.WaitingHistoryAsync(item.TaskId);
    }
    private async void OnStartWaiting(object sender, RoutedEventArgs e)
    {
        var id = (WaitingGrid.SelectedItem as WaitingTask)?.TaskId ?? _workflowSelectedTask?.Id;
        if (id is null || WaitingFollowupPicker.SelectedDate is not DateTime followup)
        { WaitingStatusText.Text = "Select a task and follow-up date."; return; }
        try
        {
            await _workflows.StartWaitingAsync(id, WaitingContactBox.Text,
                WaitingExpectedPicker.SelectedDate is DateTime expected ? DateOnly.FromDateTime(expected) : null,
                DateOnly.FromDateTime(followup), TimeZoneInfo.Local.Id, WaitingNoteBox.Text);
            WaitingNoteBox.Clear(); await RefreshWorkflowsAsync(); await ReconcileRemindersAsync();
        }
        catch (Exception ex) { WaitingStatusText.Text = ex is ArgumentException ? ex.Message : "Waiting state could not be saved."; }
    }
    private async void OnResolveWaiting(object sender, RoutedEventArgs e)
    {
        if (WaitingGrid.SelectedItem is not WaitingTask item) return;
        try { await _workflows.ResolveWaitingAsync(item.TaskId, WaitingNoteBox.Text); WaitingNoteBox.Clear(); await RefreshWorkflowsAsync(); }
        catch (Exception) { WaitingStatusText.Text = "Waiting state could not be resolved."; }
    }
    private async void OnAddRecurring(object sender, RoutedEventArgs e)
    {
        if (_workflowSelectedTask is null || TaskRepeatDate.SelectedDate is not DateTime date) { WaitingStatusText.Text = "Select a source task and date."; return; }
        try
        {
            await _workflows.AddRecurringAsync(_workflowSelectedTask.Id, ((ComboBoxItem)TaskRepeatBox.SelectedItem).Content.ToString()!, DateOnly.FromDateTime(date));
            await _workflows.ReconcileRecurringAsync(DateOnly.FromDateTime(DateTime.Today)); await RefreshTasksAsync();
            WaitingStatusText.Text = "Recurrence saved. Due instances were created.";
        }
        catch (Exception ex) { WaitingStatusText.Text = ex is ArgumentException ? ex.Message : "Recurrence could not be saved."; }
    }
    private async void OnAddDependency(object sender, RoutedEventArgs e)
    {
        if (_workflowSelectedTask is null || DependencyTaskBox.SelectedItem is not WorkTask prerequisite) { WaitingStatusText.Text = "Select a task and a prerequisite."; return; }
        try { await _workflows.AddDependencyAsync(_workflowSelectedTask.Id, prerequisite.Id); WaitingStatusText.Text = "Dependency saved. Completion now waits for the prerequisite."; }
        catch (Exception ex) { WaitingStatusText.Text = ex is ArgumentException ? ex.Message : "Dependency could not be saved."; }
    }
}
