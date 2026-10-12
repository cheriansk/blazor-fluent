# BlazorFluent.UI.Templates - Enterprise Wizard Framework

A decoupled, reusable Razor Class Library (RCL) providing multi-step wizard templates and components built natively on **Microsoft Fluent UI Blazor V5**.

---

## Key Enterprise Architectural Features
1. **Modal Popup Experience**: Runs seamlessly as a modern modal dialog without requiring full-page navigation.
2. **Dual-Layer Data Protection**:
   - **By-Reference State Preservation**: Wizard state (`TState`) is held in `WizardContext<TState>` throughout step transitions, preventing DOM or form input evaporation.
   - **Backdrop Lock**: Clicking outside the modal backdrop is locked to prevent accidental dismissal.
   - **Dirty Form Guard**: When `Context.IsDirty` is flagged, attempting to cancel or dismiss displays an explicit confirmation dialog (*"Discard Unsaved Changes?"* vs *"Keep Editing"*).
3. **Declarative Step Composition**: Steps are authored simply as `<WizardStep>` tags within the parent `<FluentWizardModal>`.
4. **Step-by-Step Validation Gate**: The wizard cannot advance to the next step unless the current step's `OnValidateAsync` function passes cleanly.
5. **NuGet Ready**: Packable into a standalone `.nupkg` via `dotnet pack` for distribution across enterprise micro-frontends.

---

## How to Create a New Wizard in 3 Simple Steps

### Step 1: Define Your Strongly-Typed State Model
Create a plain C# class representing the cumulative data collected by the wizard:

```csharp
public class OnboardingWizardState
{
    // Step 1 data
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Step 2 data
    public string SelectedRole { get; set; } = "Dev";
    public List<string> AssignedTenants { get; set; } = new();

    // Step 3 data
    public bool AcceptedPolicies { get; set; }
}
```

---

### Step 2: Use `<FluentWizardModal>` in Your Page or Component
Nest `<WizardStep>` child tags containing your step markup or sub-components. Each step can define validation:

```razor
@using BlazorFluent.UI.Templates.Wizards

<FluentButton Appearance="ButtonAppearance.Primary" OnClick="@(() => _isWizardOpen = true)">
    Start Onboarding
</FluentButton>

<FluentWizardModal TState="OnboardingWizardState"
                   @bind-IsOpen="_isWizardOpen"
                   Title="Employee Onboarding"
                   Subtitle="Provision account, role, and security access"
                   InitialState="@_model"
                   FinishButtonText="Complete Onboarding"
                   OnFinish="HandleOnboardingFinishedAsync"
                   OnCancel="HandleOnboardingCancelled">
    <Steps>
        @* Step 1: Profile *@
        <WizardStep Title="Profile" Summary="Contact details" OnValidateAsync="@ValidateStep1Async">
            <FluentStack Orientation="Orientation.Vertical" VerticalGap="12">
                <FluentTextInput Label="Full Name" @bind-Value="@_model.FullName" Required="true" />
                <FluentTextInput Label="Email" @bind-Value="@_model.Email" Required="true" />
            </FluentStack>
        </WizardStep>

        @* Step 2: Roles *@
        <WizardStep Title="Roles" Summary="Access level" OnValidateAsync="@ValidateStep2Async">
            <FluentStack Orientation="Orientation.Vertical" VerticalGap="12">
                <FluentLabel>Select security role for @_model.FullName</FluentLabel>
                <FluentSelect TOption="string" TValue="string" Items="@_availableRoles" @bind-Value="@_model.SelectedRole" />
            </FluentStack>
        </WizardStep>

        @* Step 3: Confirmation *@
        <WizardStep Title="Review" Summary="Final check">
            <FluentCard Style="padding: 16px;">
                <FluentLabel Typo="Typography.H6">Ready to provision user:</FluentLabel>
                <div><strong>Name:</strong> @_model.FullName</div>
                <div><strong>Email:</strong> @_model.Email</div>
                <div><strong>Role:</strong> @_model.SelectedRole</div>
            </FluentCard>
        </WizardStep>
    </Steps>
</FluentWizardModal>

@code {
    private bool _isWizardOpen = false;
    private OnboardingWizardState _model = new();
    private List<string> _availableRoles = new() { "Admin", "Dev", "QA", "ReadOnly" };

    private Task<bool> ValidateStep1Async()
    {
        if (string.IsNullOrWhiteSpace(_model.FullName) || string.IsNullOrWhiteSpace(_model.Email))
        {
            throw new InvalidOperationException("Please provide both Full Name and Email before proceeding.");
        }
        return Task.FromResult(true);
    }

    private Task<bool> ValidateStep2Async()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(_model.SelectedRole));
    }

    private async Task HandleOnboardingFinishedAsync(OnboardingWizardState state)
    {
        // Call domain service to persist user
        await UserService.ProvisionUserAsync(state);
    }

    private void HandleOnboardingCancelled()
    {
        // Reset state or log cancellation
        _model = new();
    }
}
```

---

### Step 3: Modularizing with Child Components (Optional)
For complex workflows (e.g., 5–8 steps), place each step's markup into its own `.razor` component (e.g. `Step1ProfileComp.razor`, `Step2RolesComp.razor`) and pass `@_model`:

```razor
<WizardStep Title="Profile" Summary="Basic Details" OnValidateAsync="@_step1Ref.ValidateAsync">
    <Step1ProfileComp @ref="_step1Ref" Model="@_model" />
</WizardStep>
```

This keeps the main page concise, decoupled, and easily testable.
