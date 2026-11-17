using System.Collections.ObjectModel;
using TubeCadCam.Application.Ports;
using TubeCadCam.Cad;
using TubeCadCam.Domain;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Presentation;

public enum ViewMode { Model3D, Section2D }

/// <summary>
/// The tube editor. Every change of profile, length or features rebuilds the CAD model, validates it and regenerates the
/// 3D scene immediately. Save/Load use the versioned model document.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IModelDocumentStore? _store;
    private readonly IFileDialogService? _dialogs;
    private Guid _partId = Guid.NewGuid();
    private string _name = "Rohr 1";
    private string _materialId = "S235";
    private double _lengthMm = 1000;
    private TubeModel? _model;
    private TubeScene? _scene;
    private IReadOnlyList<ValidationIssue> _issues = [];
    private FeatureViewModel? _selected;
    private ViewMode _viewMode = ViewMode.Model3D;
    private string _status = "Bereit.";
    private string? _filePath;
    private bool _isDirty;
    private bool _suspend;
    private string _libraryDesignation = string.Empty;

    public MainViewModel() : this(null, null, null)
    {
    }

    public MainViewModel(IModelDocumentStore? store, IProfileCatalogProvider? catalogs, IFileDialogService? dialogs)
    {
        _store = store;
        _dialogs = dialogs;
        ProfileEditor = new ProfileEditorViewModel();
        ProfileEditor.ProfileChanged += (_, _) => OnEdited();
        Library = catalogs is null ? null : new ProfileLibraryViewModel(catalogs);
        Camera = new OrbitCamera();
        FeatureKinds = Enum.GetValues<FeatureKind>();

        AddFeatureCommand = new RelayCommand(p => AddFeature((FeatureKind)p!));
        RemoveFeatureCommand = new RelayCommand(_ => RemoveSelectedFeature(), _ => SelectedFeature is not null);
        DuplicateFeatureCommand = new RelayCommand(_ => DuplicateSelectedFeature(), _ => SelectedFeature is not null);
        NewCommand = new RelayCommand(_ => NewDocument());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(saveAs: false), ReportError);
        SaveAsCommand = new AsyncRelayCommand(_ => SaveAsync(saveAs: true), ReportError);
        OpenCommand = new AsyncRelayCommand(_ => OpenAsync(), ReportError);
        ApplyLibraryEntryCommand = new RelayCommand(_ => ApplyLibraryEntry(), _ => Library?.Selected is not null);
        SaveToLibraryCommand = new AsyncRelayCommand(_ => SaveToLibraryAsync(), ReportError);
        FitViewCommand = new RelayCommand(_ => FitView());
        SetViewCommand = new RelayCommand(p => { Camera.SetPreset((ViewPreset)p!); });
        ToggleViewModeCommand = new RelayCommand(_ => ViewMode = ViewMode == ViewMode.Model3D ? ViewMode.Section2D : ViewMode.Model3D);

        Rebuild();
        FitView();
        _isDirty = false;
    }

    public ProfileEditorViewModel ProfileEditor { get; }
    public ProfileLibraryViewModel? Library { get; }
    public OrbitCamera Camera { get; }
    public IReadOnlyList<FeatureKind> FeatureKinds { get; }
    public ObservableCollection<FeatureViewModel> Features { get; } = [];

    public System.Windows.Input.ICommand AddFeatureCommand { get; }
    public System.Windows.Input.ICommand RemoveFeatureCommand { get; }
    public System.Windows.Input.ICommand DuplicateFeatureCommand { get; }
    public System.Windows.Input.ICommand NewCommand { get; }
    public System.Windows.Input.ICommand SaveCommand { get; }
    public System.Windows.Input.ICommand SaveAsCommand { get; }
    public System.Windows.Input.ICommand OpenCommand { get; }
    public System.Windows.Input.ICommand ApplyLibraryEntryCommand { get; }
    public System.Windows.Input.ICommand SaveToLibraryCommand { get; }
    public System.Windows.Input.ICommand FitViewCommand { get; }
    public System.Windows.Input.ICommand SetViewCommand { get; }
    public System.Windows.Input.ICommand ToggleViewModeCommand { get; }

    public string Title => "Tube CAD/CAM" + (_filePath is null ? string.Empty : " – " + Path.GetFileName(_filePath)) + (_isDirty ? " *" : string.Empty);

    public string Name { get => _name; set { if (Set(ref _name, value)) { OnEdited(); } } }
    public string MaterialId { get => _materialId; set { if (Set(ref _materialId, value)) { OnEdited(); } } }
    public double LengthMm { get => _lengthMm; set { if (Set(ref _lengthMm, value)) { OnEdited(); } } }
    public string LibraryDesignation { get => _libraryDesignation; set => Set(ref _libraryDesignation, value); }

    public TubeModel? Model { get => _model; private set => Set(ref _model, value); }
    public TubeScene? Scene { get => _scene; private set => Set(ref _scene, value); }
    public IReadOnlyList<ValidationIssue> Issues { get => _issues; private set => Set(ref _issues, value); }
    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsDirty => _isDirty;
    public string? FilePath => _filePath;

    public ViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (Set(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(Is3D));
                OnPropertyChanged(nameof(Is2D));
            }
        }
    }

    public bool Is3D => ViewMode == ViewMode.Model3D;
    public bool Is2D => ViewMode == ViewMode.Section2D;

    public FeatureViewModel? SelectedFeature
    {
        get => _selected;
        set
        {
            FeatureViewModel? old = _selected;
            if (Set(ref _selected, value))
            {
                if (old is not null) { old.IsSelected = false; }
                if (value is not null) { value.IsSelected = true; }
                (RemoveFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DuplicateFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Called by the 3D view when the user clicks a feature (or empty space with null).</summary>
    public void SelectFeature(Guid? id) => SelectedFeature = id is null ? null : Features.FirstOrDefault(f => f.Id == id);

    // ---- feature editing ----

    public FeatureViewModel AddFeature(FeatureKind kind)
    {
        var vm = new FeatureViewModel(kind, Guid.NewGuid(), FeatureDescriptors.Defaults(kind, LengthMm), TubeEnd.Start,
            FeatureDescriptors.DefaultContourText, OnEdited);
        Features.Add(vm);
        Renumber();
        SelectedFeature = vm;
        OnEdited();
        return vm;
    }

    public void RemoveSelectedFeature()
    {
        if (SelectedFeature is null) { return; }
        int idx = Features.IndexOf(SelectedFeature);
        Features.Remove(SelectedFeature);
        Renumber();
        SelectedFeature = Features.Count == 0 ? null : Features[Math.Min(idx, Features.Count - 1)];
        OnEdited();
    }

    public void DuplicateSelectedFeature()
    {
        if (SelectedFeature is null) { return; }
        var src = SelectedFeature;
        var copy = new FeatureViewModel(src.Kind, Guid.NewGuid(), src.Parameters.Select(p => (p.Name, p.Value)), src.End, src.ContourText, OnEdited);
        Features.Add(copy);
        Renumber();
        SelectedFeature = copy;
        OnEdited();
    }

    private void Renumber()
    {
        for (int i = 0; i < Features.Count; i++)
        {
            Features[i].DisplayName = $"{i + 1}. {FeatureDescriptors.Label(Features[i].Kind)}";
        }
    }

    // ---- model build ----

    private void OnEdited()
    {
        if (_suspend) { return; }
        _isDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Title));
        Rebuild();
    }

    private void Rebuild()
    {
        var issues = new List<ValidationIssue>();
        ProfileDefinition? profile = ProfileEditor.Definition;
        if (profile is null || !ProfileEditor.IsValid)
        {
            issues.AddRange(ProfileEditor.Issues);
            Model = null;
            Scene = null;
            Issues = issues;
            OnPropertyChanged(nameof(HasErrors));
            return;
        }

        var features = new List<TubeFeature>();
        foreach (FeatureViewModel f in Features)
        {
            (TubeFeature? feature, IReadOnlyList<ValidationIssue> fi) = f.Build();
            issues.AddRange(fi);
            if (feature is not null) { features.Add(feature); }
        }

        var model = new TubeModel(new TubePart(_partId, _name, _materialId, profile, _lengthMm), features);
        issues.AddRange(model.Validate());
        Model = model;
        Result<TubeScene> scene = TubeSceneBuilder.Build(model);
        Scene = scene.IsSuccess ? scene.Value : null;
        Issues = issues;
        OnPropertyChanged(nameof(HasErrors));
    }

    // ---- document handling ----

    public void NewDocument()
    {
        _suspend = true;
        try
        {
            _partId = Guid.NewGuid();
            Name = "Rohr 1";
            MaterialId = "S235";
            LengthMm = 1000;
            ProfileEditor.Load(new RoundTubeProfile(60, 3));
            Features.Clear();
            SelectedFeature = null;
            _filePath = null;
        }
        finally { _suspend = false; }

        Rebuild();
        FitView();
        MarkClean("Neues Modell.");
    }

    public void LoadModel(TubeModel model, string? path)
    {
        _suspend = true;
        try
        {
            _partId = model.Part.Id;
            Name = model.Part.Name;
            MaterialId = model.Part.MaterialId;
            LengthMm = model.Part.LengthMm;
            ProfileEditor.Load(model.Part.Profile);
            Features.Clear();
            foreach (TubeFeature f in model.Features)
            {
                Features.Add(FeatureViewModel.FromFeature(f, OnEdited));
            }

            Renumber();
            SelectedFeature = null;
            _filePath = path;
        }
        finally { _suspend = false; }

        Rebuild();
        FitView();
        MarkClean(path is null ? "Modell geladen." : $"Geladen: {path}");
    }

    private void MarkClean(string status)
    {
        _isDirty = false;
        Status = status;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Title));
    }

    public async Task SaveAsync(bool saveAs, CancellationToken ct = default)
    {
        if (_store is null || _dialogs is null) { Status = "Speichern ist nicht verfügbar (kein Dokumentenspeicher)."; return; }
        if (Model is null) { Status = "Speichern nicht möglich: das Profil ist ungültig."; return; }
        string? path = saveAs || _filePath is null ? _dialogs.PickFileToSave(string.IsNullOrWhiteSpace(Name) ? "rohr" : Name) : _filePath;
        if (path is null) { return; }
        Result r = await _store.SaveAsync(path, Model, ct);
        if (!r.IsSuccess) { Status = "Speichern fehlgeschlagen: " + r.Issues[0].Message; return; }
        _filePath = path;
        MarkClean(HasErrors ? $"Gespeichert (mit Validierungsfehlern): {path}" : $"Gespeichert: {path}");
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (_store is null || _dialogs is null) { Status = "Öffnen ist nicht verfügbar (kein Dokumentenspeicher)."; return; }
        string? path = _dialogs.PickFileToOpen();
        if (path is null) { return; }
        await OpenPathAsync(path, ct);
    }

    public async Task OpenPathAsync(string path, CancellationToken ct = default)
    {
        if (_store is null) { return; }
        Result<TubeModel> r = await _store.LoadAsync(path, ct);
        if (!r.IsSuccess) { Status = "Öffnen fehlgeschlagen: " + r.Issues[0].Message; return; }
        LoadModel(r.Value, path);
    }

    // ---- library ----

    public void ApplyLibraryEntry()
    {
        if (Library?.Selected is not { } item) { return; }
        ProfileEditor.Load(item.Entry.Profile);
        Status = $"Profil '{item.Entry.Designation}' übernommen" + (item.Entry.Verified ? "." : " (nicht verifiziert).");
    }

    public async Task SaveToLibraryAsync()
    {
        if (Library is null) { Status = "Keine Bibliothek verfügbar."; return; }
        if (ProfileEditor.Definition is null || !ProfileEditor.IsValid) { Status = "Nur gültige Profile können gespeichert werden."; return; }
        if (string.IsNullOrWhiteSpace(LibraryDesignation)) { Status = "Bitte eine Bezeichnung für das Bibliotheksprofil eingeben."; return; }
        Result r = await Library.AddUserEntryAsync(LibraryDesignation, ProfileEditor.Definition);
        Status = r.IsSuccess ? $"Profil '{LibraryDesignation.Trim()}' in der Benutzerbibliothek gespeichert." : "Bibliothek: " + r.Issues[0].Message;
    }

    public void FitView()
    {
        if (Scene is not null) { Camera.Fit(Scene.Bounds); }
    }

    private void ReportError(Exception ex) => Status = "Fehler: " + ex.Message;
}
