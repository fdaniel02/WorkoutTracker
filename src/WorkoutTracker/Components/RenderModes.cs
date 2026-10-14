using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace WorkoutTracker.Components;

public static class RenderModes
{
    public static readonly IComponentRenderMode InteractiveServerNoPrerender = new InteractiveServerRenderMode(prerender: false);
}
