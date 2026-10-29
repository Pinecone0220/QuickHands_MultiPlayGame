using UnityEngine;
using UnityEngine.UIElements;

namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private Image viewModelImage;
        internal void SetViewModelTexture(Texture texture)
        {
            if (root == null) return;
            if (viewModelImage == null)
            {
                viewModelImage = new Image { name = "first-person-model", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
                viewModelImage.style.position = Position.Absolute;
                viewModelImage.style.left = viewModelImage.style.right = viewModelImage.style.top = viewModelImage.style.bottom = 0;
                root.Add(viewModelImage);
            }
            viewModelImage.image = texture;
            Show(viewModelImage, texture != null);
            OrderViewModel();
        }
        private void OrderViewModel()
        {
            if (viewModelImage != null && nameplateLayer != null)
                viewModelImage.PlaceInFront(nameplateLayer);
        }
    }
}
