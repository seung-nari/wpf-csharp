using System;
using System.Collections.Generic;

namespace MiniBoard;

public class BoardService
{
    private readonly IPostRepository _repo;

    public BoardService(IPostRepository repo)
    {
        _repo = repo;
    }

    public List<Post> GetList()
        => _repo.GetAll();
    
    public Post GetDetail(int id)
        => _repo.GetById(id);
    
    public Post Create(string title, string content, string author)
    {
        if(string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("제목은 필수입니다.");
        
        var post = new Post
        {
            Title = title.Trim(),
            Content = content ?? "",
            Author = string.IsNullOrWhiteSpace(author) ? "익명" : author
        };

        return _repo.Add(post);
    }

    public void Delete(int id)
        => _repo.Delete(id);
}