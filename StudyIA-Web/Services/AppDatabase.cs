using System.Text.Json;
using Microsoft.JSInterop;
using StudyIA_Web.Models;

namespace StudyIA_Web.Services;

/// <summary>
/// Almacenamiento local basado en localStorage del navegador.
/// Los datos se cargan en memoria al arrancar (InitializeAsync) y se
/// persisten como JSON en localStorage tras cada operación de escritura.
/// </summary>
public class AppDatabase
{
    private readonly IJSRuntime _js;

    private List<FolderRecord>         _folders     = new();
    private List<PdfFileRecord>        _pdfFiles    = new();
    private List<QuestionRecord>       _questions   = new();
    private List<UserAnswerRecord>     _userAnswers = new();
    private List<PdfSection>           _sections    = new();
    private Dictionary<string, string> _settings    = new();

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    private const string KeyFolders     = "studyia_folders";
    private const string KeyPdfFiles    = "studyia_pdffiles";
    private const string KeyQuestions   = "studyia_questions";
    private const string KeyUserAnswers = "studyia_useranswers";
    private const string KeySections    = "studyia_sections";
    private const string KeySettings    = "studyia_settings";

    public AppDatabase(IJSRuntime js) => _js = js;

    /// <summary>Carga todos los datos desde localStorage. Llamar una vez en OnInitializedAsync.</summary>
    public async Task InitializeAsync()
    {
        _folders     = await Load<List<FolderRecord>>(KeyFolders)                 ?? new();
        _pdfFiles    = await Load<List<PdfFileRecord>>(KeyPdfFiles)               ?? new();
        _questions   = await Load<List<QuestionRecord>>(KeyQuestions)             ?? new();
        _userAnswers = await Load<List<UserAnswerRecord>>(KeyUserAnswers)         ?? new();
        _sections    = await Load<List<PdfSection>>(KeySections)                  ?? new();
        _settings    = await Load<Dictionary<string, string>>(KeySettings)       ?? new();
    }

    // ── Folders ────────────────────────────────────────────────────────────

    public List<FolderRecord> GetAllFolders() =>
        _folders.OrderBy(f => f.Name).ToList();

    public async Task<int> AddFolderAsync(string name, string folderPath)
    {
        var existing = _folders.FirstOrDefault(f => f.FolderPath == folderPath);
        if (existing is not null) return existing.Id;

        var record = new FolderRecord
        {
            Id         = NextId(_folders, f => f.Id),
            Name       = name,
            FolderPath = folderPath,
            CreatedAt  = DateTime.UtcNow
        };
        _folders.Add(record);
        await Save(KeyFolders, _folders);
        return record.Id;
    }

    /// <summary>Elimina el temario y en cascada sus PDFs, secciones, preguntas y respuestas.</summary>
    public async Task DeleteFolderAsync(int folderId)
    {
        var folder = _folders.FirstOrDefault(f => f.Id == folderId);
        if (folder is null) return;

        var pdfIds = _pdfFiles
            .Where(p => p.FolderPath == folder.FolderPath)
            .Select(p => p.Id)
            .ToHashSet();

        var questionIds = _questions
            .Where(q => pdfIds.Contains(q.PdfFileId))
            .Select(q => q.Id)
            .ToHashSet();

        _userAnswers.RemoveAll(a => questionIds.Contains(a.QuestionId));
        _questions  .RemoveAll(q => pdfIds.Contains(q.PdfFileId));
        _sections   .RemoveAll(s => pdfIds.Contains(s.PdfFileId));
        _pdfFiles   .RemoveAll(p => p.FolderPath == folder.FolderPath);
        _folders    .Remove(folder);

        await Save(KeyFolders,     _folders);
        await Save(KeyPdfFiles,    _pdfFiles);
        await Save(KeyQuestions,   _questions);
        await Save(KeyUserAnswers, _userAnswers);
        await Save(KeySections,    _sections);
    }

    // ── Settings ───────────────────────────────────────────────────────────

    public string? GetSetting(string key) =>
        _settings.TryGetValue(key, out var v) ? v : null;

    public async Task SetSettingAsync(string key, string value)
    {
        _settings[key] = value;
        await Save(KeySettings, _settings);
    }

    // ── PdfFiles ───────────────────────────────────────────────────────────

    public async Task<int> UpsertPdfFileAsync(string folderPath, string filePath,
                                              string fileName, string hash, long fileSize)
    {
        var existing = _pdfFiles.FirstOrDefault(p => p.FilePath == filePath);
        if (existing is null)
        {
            existing = new PdfFileRecord
            {
                Id         = NextId(_pdfFiles, p => p.Id),
                FolderPath = folderPath,
                FilePath   = filePath,
                FileName   = fileName,
                FileHash   = hash,
                FileSize   = fileSize,
                LastSeen   = DateTime.UtcNow
            };
            _pdfFiles.Add(existing);
        }
        else
        {
            existing.FileName = fileName;
            existing.FileHash = hash;
            existing.FileSize = fileSize;
            existing.LastSeen = DateTime.UtcNow;
        }
        await Save(KeyPdfFiles, _pdfFiles);
        return existing.Id;
    }

    /// <summary>Devuelve el hash almacenado del archivo (o null si no existe). Sync — in-memory.</summary>
    public string? GetStoredHash(string filePath) =>
        _pdfFiles.FirstOrDefault(p => p.FilePath == filePath)?.FileHash;

    public List<PdfFileRecord> GetFilesInFolder(string folderPath) =>
        _pdfFiles.Where(p => p.FolderPath == folderPath)
                 .OrderBy(p => p.FileName)
                 .ToList();

    public int GetQuestionCount(int pdfFileId) =>
        _questions.Count(q => q.PdfFileId == pdfFileId);

    public List<(int PdfFileId, string FileName, int Count)> GetQuestionSummary(string folderPath) =>
        _pdfFiles.Where(p => p.FolderPath == folderPath)
                 .OrderBy(p => p.FileName)
                 .Select(p => (p.Id, p.FileName, _questions.Count(q => q.PdfFileId == p.Id)))
                 .ToList();

    public List<QuestionRecord> GetAllQuestionsForFolder(string folderPath)
    {
        var pdfIds  = _pdfFiles.Where(p => p.FolderPath == folderPath).Select(p => p.Id).ToHashSet();
        var fileMap = _pdfFiles.ToDictionary(p => p.Id, p => p.FileName);

        var list = _questions
            .Where(q => pdfIds.Contains(q.PdfFileId))
            .Select(q => q.WithFileName(fileMap.GetValueOrDefault(q.PdfFileId, q.FileName)))
            .ToList();

        // Fisher-Yates shuffle
        var rng = new Random();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // ── Questions ──────────────────────────────────────────────────────────

    public async Task<int> SaveQuestionAsync(int pdfFileId, int pageNumber, string context,
                                             string questionText, string expectedAnswer)
    {
        var sectionId = _sections
            .Where(s => s.PdfFileId == pdfFileId && s.StartPage <= pageNumber && s.EndPage >= pageNumber)
            .Select(s => (int?)s.Id)
            .FirstOrDefault();
        return await SaveQuestionCoreAsync(pdfFileId, sectionId, pageNumber, context, questionText, expectedAnswer);
    }

    public async Task<int> SaveQuestionForSectionAsync(int pdfFileId, int pdfSectionId,
                                                       int pageNumber, string context,
                                                       string questionText, string expectedAnswer)
        => await SaveQuestionCoreAsync(pdfFileId, pdfSectionId, pageNumber, context, questionText, expectedAnswer);

    private async Task<int> SaveQuestionCoreAsync(int pdfFileId, int? pdfSectionId, int pageNumber,
                                                   string context, string questionText, string expectedAnswer)
    {
        var record = new QuestionRecord
        {
            Id             = NextId(_questions, q => q.Id),
            PdfFileId      = pdfFileId,
            PdfSectionId   = pdfSectionId,
            PageNumber     = pageNumber,
            Context        = context,
            QuestionText   = questionText,
            ExpectedAnswer = expectedAnswer,
            CreatedAt      = DateTime.UtcNow,
            FileName       = _pdfFiles.FirstOrDefault(p => p.Id == pdfFileId)?.FileName ?? string.Empty
        };
        _questions.Add(record);
        await Save(KeyQuestions, _questions);
        return record.Id;
    }

    public List<QuestionRecord> GetQuestionsForFile(int pdfFileId)
    {
        var fileName = _pdfFiles.FirstOrDefault(p => p.Id == pdfFileId)?.FileName ?? string.Empty;
        return _questions
            .Where(q => q.PdfFileId == pdfFileId)
            .OrderBy(q => q.PageNumber).ThenBy(q => q.Id)
            .Select(q => q.WithFileName(fileName))
            .ToList();
    }

    // ── UserAnswers ────────────────────────────────────────────────────────

    public async Task<int> SaveUserAnswerAsync(int questionId, string userAnswer,
                                               double score, string feedback)
    {
        var record = new UserAnswerRecord
        {
            Id         = NextId(_userAnswers, a => a.Id),
            QuestionId = questionId,
            UserAnswer = userAnswer,
            Score      = score,
            Feedback   = feedback,
            AnsweredAt = DateTime.UtcNow
        };
        _userAnswers.Add(record);
        await Save(KeyUserAnswers, _userAnswers);
        return record.Id;
    }

    public List<UserAnswerRecord> GetAnswersForQuestion(int questionId) =>
        _userAnswers.Where(a => a.QuestionId == questionId)
                    .OrderByDescending(a => a.AnsweredAt)
                    .ToList();

    // ── PdfSections ────────────────────────────────────────────────────────

    public int GetQuestionCountForSection(int pdfSectionId) =>
        _questions.Count(q => q.PdfSectionId == pdfSectionId);

    public bool HasSections(int pdfFileId) =>
        _sections.Any(s => s.PdfFileId == pdfFileId);

    public async Task SaveSectionsAsync(int pdfFileId, List<PdfSection> sections)
    {
        _sections.RemoveAll(s => s.PdfFileId == pdfFileId);
        var nextId = NextId(_sections, s => s.Id);
        foreach (var s in sections)
        {
            s.Id        = nextId++;
            s.PdfFileId = pdfFileId;
            _sections.Add(s);
        }
        await Save(KeySections, _sections);
    }

    public List<PdfSection> GetSections(int pdfFileId) =>
        _sections.Where(s => s.PdfFileId == pdfFileId)
                 .OrderBy(s => s.StartPage)
                 .ToList();

    // ── Helpers privados ───────────────────────────────────────────────────

    private async Task<T?> Load<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, _jsonOpts);
    }

    private async Task Save<T>(string key, T data) =>
        await _js.InvokeVoidAsync("localStorage.setItem", key,
                                  JsonSerializer.Serialize(data, _jsonOpts));

    private static int NextId<T>(List<T> list, Func<T, int> selector) =>
        list.Count == 0 ? 1 : list.Max(selector) + 1;
}
