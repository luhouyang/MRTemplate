using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the pottery & dogu for one group
/// Enables quick & easy reordering of pottery & dogu
/// 
/// 1つのグループに属する土器と土偶を保持します。
/// 土器と土偶の素早く簡単な並べ替えを可能にします。
/// </summary>
public class GroupItems : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> models = new List<GameObject>();

    [Header("Meseum Experience")]
    [SerializeField] private bool isMuseumExperience = false;
    [SerializeField] private GameObject museumArrangement;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public List<GameObject> GetModels()
    {
        return models;
    }
}
