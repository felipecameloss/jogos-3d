using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pool de prefabs (projéteis, inimigos, migalhas, partículas).
/// Get() ativa o objeto, Release() desativa e guarda para reaproveitar.
/// </summary>
public class ComponentPool<T> where T : Component
{
    readonly T prefab;
    readonly Transform root;
    readonly ObjectPool<T> pool;

    public int CountAll => pool.CountAll;
    public int CountActive => pool.CountActive;
    public int CountInactive => pool.CountInactive;

    /// <param name="prewarm">Quantos criar já no início (evita travadinha no primeiro uso).</param>
    /// <param name="maxSize">Acima disso, os devolvidos são destruídos em vez de guardados.</param>
    public ComponentPool(T prefab, int prewarm = 0, int maxSize = 500, string rootName = null)
    {
        this.prefab = prefab;
        root = new GameObject(rootName ?? $"[Pool] {prefab.name}").transform;
        pool = new ObjectPool<T>(
            Create,
            null, // ativa no Get, depois de posicionar
            item => item.gameObject.SetActive(false),
            item => { if (item != null) Object.Destroy(item.gameObject); },
            collectionCheck: Application.isEditor, // acusa Release duplo no editor
            defaultCapacity: Mathf.Max(prewarm, 10),
            maxSize: maxSize);

        if (prewarm > 0)
        {
            var temp = new T[prewarm];
            for (int i = 0; i < prewarm; i++) temp[i] = pool.Get();
            for (int i = 0; i < prewarm; i++) pool.Release(temp[i]);
        }
    }

    T Create()
    {
        var item = Object.Instantiate(prefab, root);
        item.gameObject.SetActive(false); // fica guardado até alguém pegar já posicionado
        item.name = prefab.name;
        return item;
    }

    public T Get()
    {
        var item = pool.Get();
        item.gameObject.SetActive(true);
        return item;
    }

    /// <summary>Posiciona antes de ativar (NavMeshAgent, trail e OnEnable já nascem no lugar certo).</summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        var item = pool.Get();
        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        return item;
    }

    public void Release(T item)
    {
        if (item == null || !item.gameObject.activeSelf) return; // já devolvido ou destruído
        pool.Release(item);
    }
}
