using UnityEngine;

public class ScrollingStarLayer : MonoBehaviour
{
    [SerializeField] private Transform[] tiles;
    [SerializeField] private float scrollSpeed = .7f;
    [SerializeField] private float tileHeight = 7f;

    private bool isScrolling = false;

    public void BeginScrolling()
    {
        isScrolling = true;
    }

    private void Update()
    {
        if(!isScrolling || tiles == null || tiles.Length == 0 || tileHeight <= 0f)
        {
            return;
        }
        float movement = scrollSpeed * Time.deltaTime;

        float totalHeight = tileHeight * tiles.Length;

        float resetBelow = -totalHeight / 2f;

        foreach(Transform tile in tiles)
        {
            if(tile == null)
            {
                continue;
            }

            Vector3 position = tile.localPosition;

            position.y -= movement;

            if(position.y < resetBelow)
            {
                position.y += totalHeight;
            }

            tile.localPosition = position;
        }
    }

    [ContextMenu("Test - Begin Scrolling")]
    private void TestBeginScrolling()
    {
        BeginScrolling();
    }
}

