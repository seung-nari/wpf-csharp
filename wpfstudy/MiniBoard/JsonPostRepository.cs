using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MiniBoard;

public class JsonPostRepository : IPostRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public JsonPostRepository(string filePath)
    {
        _filePath = filePath;
    }

    private PostStore Load()
    {
        if (!File.Exists(_filePath))
            return new PostStore {LastId = 0};
        
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<PostStore>(json) ?? new PostStore();
    }

    private void Save(PostStore store)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(store, options);
        File.WriteAllText(_filePath, json);
    }

    public List<Post> GetAll()
    {
        lock (_lock)
        {
            var store = Load();
            return store.Posts
                .OrderByDescending(p => p.CreatedAt)
                .ToList();
        }
    }

    public Post GetById(int id)
    {
        lock (_lock)
        {
            var store = Load();
            return store.Posts.FirstOrDefault(p => p.Id == id);
        }
    }

    public Post Add(Post newPost)
    {
        lock (_lock)
        {
            var store = Load();
            var nextId = store.LastId + 1;

            newPost.Id = nextId;
            newPost.CreatedAt = DateTime.Now;

            store.Posts.Add(newPost);
            store.LastId = nextId;

            Save(store);
            return newPost;
        }
    }

    public void Update(Post post)
    {
        lock (_lock)
        {
            var store = Load();
            var target = store.Posts.FirstOrDefault(p => p.Id == post.Id);
            if (target == null) return;

            target.Title    = post.Title;
            target.Content  = post.Content;
            target.Author   = post.Author;
            target.UpdatedAt    = DateTime.Now;

            Save(store);
        }
    }

    public void Delete(int id)
    {
        lock (_lock)
        {
            var store = Load();
            var target = store.Posts.FirstOrDefault(p => p.Id == id);
            if (target == null) return;

            store.Posts.Remove(target);
            Save(store);
        }
    }
}