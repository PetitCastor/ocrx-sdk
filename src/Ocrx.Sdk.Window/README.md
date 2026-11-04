# Ocrx.Sdk.Window

Window host for a plugin's own settings window. `PluginWindowHost` runs a WinForms `Form` on a
background STA thread and handles the plumbing: the form is built on first use, a second
`ShowAsync()` restores and activates it, closing it hides it, and `Dispose()` closes it and ends the
thread. It also owns the theme (system, light or dark; system follows Windows) and the window icon.

## Usage

Declare the window in the plugin, create the host with a form factory, and open it when the engine
asks:

```csharp
public bool HasSettingsWindow => true;

private readonly PluginWindowHost _window;

public MyPlugin() =>
    _window = new PluginWindowHost(() => new SettingsForm("My plugin", CurrentFields, OnEditAsync));

public Task OnOpenSettings(IPluginServices services, CancellationToken ct) => _window.ShowAsync();
```

`SettingsForm` draws the fields you declare (`SelectField`, `ToggleField`, `IntField`, `FloatField`,
`StringField`, `GroupField`, `HelpField`). An int or float field with both `Min` and `Max` is a
slider with a value readout. Each committed edit reaches your callback, which validates, applies and
persists it:

```csharp
private Task<SettingsResult> OnEditAsync(SettingsEdit edit)
{
    if (!TryApply(edit.FieldId, edit.Value, out var error))
        return Task.FromResult(SettingsResult.Fail(error));   // the field reverts and shows the message

    return Task.FromResult(SettingsResult.Refresh(CurrentFields()));
}
```

`CurrentFields` is read when the form is built and again each time the window is shown after being
hidden, so it should return the plugin's current values. It and the edit callback both run on the
window's UI thread, so neither may block. Edits are handled one at a time; one still waiting (or typed
and not yet sent) goes out after the running one, and when the window loses focus or is closed. A
`Refresh` rebuilds the controls, keeping the scroll position, the focused field and unsent edits. A
`Refresh` the form cannot draw (duplicate ids, a slider range that is not a whole number of steps) is
shown as a `Fail` on the edited field.

`PluginWindowHost` takes any `Func<Form>`, so a plugin can ignore `SettingsForm` and hand it its own
window. The host sets the form's `BackColor` and `ForeColor` on every theme change, so a custom form
should not set its own top-level colours; read `host.Theme` to colour child controls. Call
`_window.Dispose()` when the plugin stops.

```csharp
_window.Theme = WindowTheme.Dark;      // or System (follows Windows) / Light
_window.Icon = myIcon;                 // the host does not own it
```
