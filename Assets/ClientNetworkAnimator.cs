using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkAnimator : NetworkAnimator
{
    // Overriding this built-in Netcode property shifts animation authority 
    // from the host server straight to the local player controlling the character
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
