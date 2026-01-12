using System.Collections.Generic;

namespace MiniBoard;

public interface IPostRepository
{
    List<Post> GetAll();
    Post GetById(int id);
    Post Add(Post newPost);
    void Update(Post post);
    void Delete(int id);
}