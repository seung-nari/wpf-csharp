using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniBoard;

namespace wpfstudy;

public class MainViewModel : ViewModelBase
{
    private readonly BoardService _service;

    public ObservableCollection<Post> Posts {get;} = new();

    private Post _selectedPost;
    public Post SelectedPost
    {
        get => _selectedPost;
        set
        {
            _selectedPost = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand {get;}
    public ICommand AddDummyCommand {get;}
    public ICommand DeleteCommand {get;}

    public MainViewModel(BoardService service)
    {
        _service = service;

        LoadCommand = new RelayCommand(_ => Load());
        AddDummyCommand = new RelayCommand(_ => AddDummy());
        DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedPost != null);
        
        SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());

        Load();
    }

    private void Load()
    {
        Posts.Clear();
        foreach(var p in _service.GetList())
            Posts.Add(p);
        
        SelectedPost = Posts.Count > 0 ? Posts[0] : null;
    }

    private void AddDummy()
    {
        _service.Create("첫 글", "WPF에서 추가된 글입니다.", "관리자");
        Load();
    }

    private void DeleteSelected()
    {
        if (SelectedPost == null) return;
        _service.Delete(SelectedPost.Id);
        Load();
    }


    // 게시글 작성 관련
    private string _newTitle = "";
    public string NewTitle
    {
        get => _newTitle;
        set 
        { 
            _newTitle = value;
            OnPropertyChanged(); 
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _newAuthor = "";
    public string NewAuthor
    {
        get => _newAuthor;
        set 
        { 
            _newAuthor = value;
            OnPropertyChanged(); 
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _newContent = "";
    public string NewContent
    {
        get => _newContent;
        set 
        { 
            _newContent = value; 
            OnPropertyChanged(); 
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand SaveCommand { get; }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(NewTitle)
            && !string.IsNullOrWhiteSpace(NewContent);
    }

    private void Save()
    {
        var created = _service.Create(NewTitle, NewContent, NewAuthor);

        // 리스트 갱신
        Load();

        // 방금 만든 글 선택(옵션)
        var createdInList = Posts.FirstOrDefault(p => p.Id == created.Id);
        if (createdInList != null)
            SelectedPost = createdInList;

        // 작성 폼 초기화
        NewTitle = "";
        NewAuthor = "";
        NewContent = "";
    }

}
