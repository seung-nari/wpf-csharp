using System.Collections.Generic; // List<T> 같은 “제네릭 컬렉션”을 쓰기 위해 필요한 using

namespace MiniBoard;

public class PostStore
{
    public int LastId {get; set;}
    public List<Post> Posts {get; set;} = new();
}