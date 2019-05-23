//#define TEMPORARY_RENDERDOC_INTEGRATION //require specific c++

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.LookDev
{
    public interface IViewDisplayer
    {
        Rect GetRect(ViewCompositionIndex index);
        void SetTexture(ViewCompositionIndex index, Texture texture);

        void Repaint();

        event Action<Layout, bool> OnLayoutChanged;

        event Action OnRenderDocAcquisitionTriggered;
        
        event Action<IMouseEvent> OnMouseEventInView;

        event Action<GameObject, ViewCompositionIndex, Vector2> OnChangingObjectInView;
        event Action<Material, ViewCompositionIndex, Vector2> OnChangingMaterialInView;
        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> OnChangingEnvironmentInView;
        
        event Action OnClosed;
    }
    
    public interface IEnvironmentDisplayer
    {
        void Repaint();

        event Action<UnityEngine.Object> OnAddingEnvironment;
        event Action<int> OnRemovingEnvironment;
        event Action<EnvironmentLibrary> OnChangingEnvironmentLibrary;
    }
    
    /// <summary>
    /// Displayer and User Interaction 
    /// </summary>
    internal class DisplayWindow : EditorWindow, IViewDisplayer, IEnvironmentDisplayer
    {
        static class Style
        {
            internal const string k_IconFolder = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/Icons/";
            internal const string k_uss = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/DisplayWindow.uss";

            public static readonly GUIContent WindowTitleAndIcon = EditorGUIUtility.TrTextContentWithIcon("Look Dev", CoreEditorUtils.LoadIcon(k_IconFolder, "LookDevMainIcon"));
        }
        
        // /!\ WARNING:
        //The following const are used in the uss.
        //If you change them, update the uss file too.
        const string k_MainContainerName = "mainContainer";
        const string k_EnvironmentContainerName = "environmentContainer";
        const string k_ViewContainerName = "viewContainer";
        const string k_FirstViewName = "firstView";
        const string k_SecondViewName = "secondView";
        const string k_ToolbarName = "toolbar";
        const string k_ToolbarRadioName = "toolbarRadio";
        const string k_ToolbarEnvironmentName = "toolbarEnvironment";
        const string k_SharedContainerClass = "container";
        const string k_FirstViewClass = "firstView";
        const string k_SecondViewsClass = "secondView";
        const string k_VerticalViewsClass = "verticalSplit";
        const string k_ShowEnvironmentPanelClass = "showEnvironmentPanel";

        VisualElement m_MainContainer;
        VisualElement m_ViewContainer;
        VisualElement m_EnvironmentContainer;
        ListView m_EnvironmentList;
        EnvironmentElement m_EnvironmentInspector;
        Image m_CurrenttlyEditedEnvironmentPreview;

        Image[] m_Views = new Image[2];
        

        Layout layout
        {
            get => LookDev.currentContext.layout.viewLayout;
            set
            {
                if (LookDev.currentContext.layout.viewLayout != value)
                {
                    OnLayoutChangedInternal?.Invoke(value, showEnvironmentPanel);
                    ApplyLayout(value);
                }
            }
        }
        
        bool showEnvironmentPanel
        {
            get => LookDev.currentContext.layout.showEnvironmentPanel;
            set
            {
                if (LookDev.currentContext.layout.showEnvironmentPanel != value)
                {
                    OnLayoutChangedInternal?.Invoke(layout, value);
                    ApplyEnvironmentToggling(value);
                }
            }
        }

        CameraController m_CameraController = new CameraController();

        event Action<Layout, bool> OnLayoutChangedInternal;
        event Action<Layout, bool> IViewDisplayer.OnLayoutChanged
        {
            add => OnLayoutChangedInternal += value;
            remove => OnLayoutChangedInternal -= value;
        }

        event Action OnRenderDocAcquisitionTriggeredInternal;
        event Action IViewDisplayer.OnRenderDocAcquisitionTriggered
        {
            add => OnRenderDocAcquisitionTriggeredInternal += value;
            remove => OnRenderDocAcquisitionTriggeredInternal -= value;
        }

        event Action<IMouseEvent> OnMouseEventInViewPortInternal;
        event Action<IMouseEvent> IViewDisplayer.OnMouseEventInView
        {
            add => OnMouseEventInViewPortInternal += value;
            remove => OnMouseEventInViewPortInternal -= value;
        }

        event Action<GameObject, ViewCompositionIndex, Vector2> OnChangingObjectInViewInternal;
        event Action<GameObject, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingObjectInView
        {
            add => OnChangingObjectInViewInternal += value;
            remove => OnChangingObjectInViewInternal -= value;
        }

        event Action<Material, ViewCompositionIndex, Vector2> OnChangingMaterialInViewInternal;
        event Action<Material, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingMaterialInView
        {
            add => OnChangingMaterialInViewInternal += value;
            remove => OnChangingMaterialInViewInternal -= value;
        }

        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> OnChangingEnvironmentInViewInternal;
        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingEnvironmentInView
        {
            add => OnChangingEnvironmentInViewInternal += value;
            remove => OnChangingEnvironmentInViewInternal -= value;
        }

        event Action OnClosedInternal;
        event Action IViewDisplayer.OnClosed
        {
            add => OnClosedInternal += value;
            remove => OnClosedInternal -= value;
        }

        event Action<UnityEngine.Object> OnAddingEnvironmentInternal;
        event Action<UnityEngine.Object> IEnvironmentDisplayer.OnAddingEnvironment
        {
            add => OnAddingEnvironmentInternal += value;
            remove => OnAddingEnvironmentInternal -= value;
        }

        event Action<int> OnRemovingEnvironmentInternal;
        event Action<int> IEnvironmentDisplayer.OnRemovingEnvironment
        {
            add => OnRemovingEnvironmentInternal += value;
            remove => OnRemovingEnvironmentInternal -= value;
        }

        event Action<EnvironmentLibrary> OnChangingEnvironmentLibraryInternal;
        event Action<EnvironmentLibrary> IEnvironmentDisplayer.OnChangingEnvironmentLibrary
        {
            add => OnChangingEnvironmentLibraryInternal += value;
            remove => OnChangingEnvironmentLibraryInternal -= value;
        }

        void OnEnable()
        {
            //Call the open function to configure LookDev
            // in case the window where open when last editor session finished.
            // (Else it will open at start and has nothing to display).
            if (!LookDev.open)
                LookDev.Open();

            titleContent = Style.WindowTitleAndIcon;

            rootVisualElement.styleSheets.Add(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(Style.k_uss));

            var RDbar = new VisualElement() { name = "RDbar" };
            Image test = new Image();
            RDbar.Add(test);
            rootVisualElement.Add(RDbar);
            test.RegisterCallback<MouseDownEvent>(evt
                => Debug.Log("click"));

            CreateToolbar();
            
            m_MainContainer = new VisualElement() { name = k_MainContainerName };
            m_MainContainer.AddToClassList(k_SharedContainerClass);
            rootVisualElement.Add(m_MainContainer);

            CreateViews();
            CreateEnvironment();
            CreateDropAreas();

            ApplyLayout(layout);
            ApplyEnvironmentToggling(showEnvironmentPanel);
            
            EditorApplication.update += ListenInputs;

        }

        void ListenInputs()
        {
            switch(LookDev.currentContext.layout.lastFocusedView)
            {
                case ViewCompositionIndex.First:
                    m_CameraController.Update(LookDev.currentContext.GetViewContent(ViewIndex.First).camera);
                    break;
                case ViewCompositionIndex.Second:
                    m_CameraController.Update(LookDev.currentContext.GetViewContent(ViewIndex.Second).camera);
                    break;
                case ViewCompositionIndex.Composite:
                    m_CameraController.Update(LookDev.currentContext.GetViewContent(ViewIndex.First).camera);
                    LookDev.currentContext.SynchronizeCameraStates(ViewIndex.First);
                    break;
            }
        }

        void OnDisable()
        {
            EditorApplication.update -= ListenInputs;
            OnClosedInternal?.Invoke();
        }

        void CreateToolbar()
        {
            // Layout swapper part
            var toolbarRadio = new ToolbarRadio() { name = k_ToolbarRadioName };
            toolbarRadio.AddRadios(new[] {
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSingle1"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSingle2"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSideBySideVertical"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSideBySideHorizontal"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSplit"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevZone"),
                });
            toolbarRadio.RegisterCallback((ChangeEvent<int> evt)
                => layout = (Layout)evt.newValue);
            toolbarRadio.SetValueWithoutNotify((int)layout);

            // Environment part
            var toolbarEnvironment = new Toolbar() { name = k_ToolbarEnvironmentName };
            var showEnvironmentToggle = new ToolbarToggle() { text = "Show Environment" };
            showEnvironmentToggle.RegisterCallback((ChangeEvent<bool> evt)
                => showEnvironmentPanel = evt.newValue);
            showEnvironmentToggle.SetValueWithoutNotify(showEnvironmentPanel);
            toolbarEnvironment.Add(showEnvironmentToggle);

            //other parts to be completed

            // Aggregate parts
            var toolbar = new Toolbar() { name = k_ToolbarName };
            toolbar.Add(new Label() { text = "Layout:" });
            toolbar.Add(toolbarRadio);
            toolbar.Add(new ToolbarSpacer());
            //to complete


            toolbar.Add(new ToolbarSpacer() { flex = true });

            //TODO: better RenderDoc integration
#if TEMPORARY_RENDERDOC_INTEGRATION
            if (UnityEditorInternal.RenderDoc.IsInstalled() && UnityEditorInternal.RenderDoc.IsLoaded())
            {
                toolbar.Add(new ToolbarButton(() => OnRenderDocAcquisitionTriggeredInternal?.Invoke())
                {
                    text = "RenderDoc Content"
                });
            }
#endif

            toolbar.Add(toolbarEnvironment);
            rootVisualElement.Add(toolbar);
        }

        void CreateViews()
        {
            if (m_MainContainer == null || m_MainContainer.Equals(null))
                throw new System.MemberAccessException("m_MainContainer should be assigned prior CreateViews()");

            m_ViewContainer = new VisualElement() { name = k_ViewContainerName };
            m_ViewContainer.AddToClassList(LookDev.currentContext.layout.isMultiView ? k_SecondViewsClass : k_FirstViewClass);
            m_ViewContainer.AddToClassList(k_SharedContainerClass);
            m_MainContainer.Add(m_ViewContainer);
            m_ViewContainer.RegisterCallback<MouseDownEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));
            m_ViewContainer.RegisterCallback<MouseUpEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));
            m_ViewContainer.RegisterCallback<MouseMoveEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));

            m_Views[(int)ViewIndex.First] = new Image() { name = k_FirstViewName, image = Texture2D.blackTexture };
            m_ViewContainer.Add(m_Views[(int)ViewIndex.First]);
            m_Views[(int)ViewIndex.Second] = new Image() { name = k_SecondViewName, image = Texture2D.blackTexture };
            m_ViewContainer.Add(m_Views[(int)ViewIndex.Second]);
        }

        void CreateDropAreas()
        {
            // GameObject or Prefab in view
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.Second, localPos));

            // Material in view
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(Material) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.Second, localPos));

            // Environment in view
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.Second, localPos));

            // Environment in library
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_EnvironmentContainer, (obj, localPos) =>
            {
                //[TODO: check if this come from outside of library]
                OnAddingEnvironmentInternal?.Invoke(obj);
            });
            new DropArea(new[] { typeof(EnvironmentLibrary) }, m_EnvironmentContainer, (obj, localPos) =>
            {
                OnChangingEnvironmentLibraryInternal?.Invoke(obj as EnvironmentLibrary);
                RefreshLibraryDisplay();
            });
        }
        
        void CreateEnvironment()
        {
            if (m_MainContainer == null || m_MainContainer.Equals(null))
                throw new System.MemberAccessException("m_MainContainer should be assigned prior CreateEnvironment()");

            m_EnvironmentContainer = new VisualElement() { name = k_EnvironmentContainerName };
            m_MainContainer.Add(m_EnvironmentContainer);
            if (showEnvironmentPanel)
                m_MainContainer.AddToClassList(k_ShowEnvironmentPanelClass);
            
            m_EnvironmentInspector = new EnvironmentElement(withPreview: false);
            m_EnvironmentList = new ListView();
            m_EnvironmentList.selectionType = SelectionType.Single;
            m_EnvironmentList.itemHeight = EnvironmentElement.k_SkyThumbnailHeight;
            m_EnvironmentList.makeItem = () =>
            {
                var preview = new Image();
                preview.AddManipulator(new EnvironmentPreviewDragger(this, m_ViewContainer));
                return preview;
            };
            m_EnvironmentList.bindItem = (e, i) =>
            {
                (e as Image).image = EnvironmentElement.GetLatLongThumbnailTexture(
                    LookDev.currentContext.environmentLibrary[i],
                    EnvironmentElement.k_SkyThumbnailWidth);
            };
            m_EnvironmentList.onSelectionChanged += objects =>
            {
                if (objects.Count == 0 || (LookDev.currentContext.environmentLibrary?.Count ?? 0) == 0)
                    m_EnvironmentInspector.style.visibility = Visibility.Hidden;
                else
                {
                    m_EnvironmentInspector.style.visibility = Visibility.Visible;
                    m_EnvironmentInspector.Bind(
                        LookDev.currentContext.environmentLibrary[m_EnvironmentList.selectedIndex],
                        m_EnvironmentList.selectedItem as Image);
                }
            };
            m_EnvironmentList.onItemChosen += obj =>
                EditorGUIUtility.PingObject(LookDev.currentContext.environmentLibrary[(int)obj]);
            m_EnvironmentContainer.Add(m_EnvironmentInspector);
            m_EnvironmentContainer.Add(m_EnvironmentList);

            RefreshLibraryDisplay();
        }

        void RefreshLibraryDisplay()
        {
            int itemMax = LookDev.currentContext.environmentLibrary?.Count ?? 0;
            var items = new List<int>(itemMax);
            for (int i = 0; i < itemMax; i++)
                items.Add(i);
            m_EnvironmentList.itemsSource = items;
            m_EnvironmentInspector.style.visibility = itemMax == 0
                ? Visibility.Hidden
                : Visibility.Visible;
            m_EnvironmentList
                .Q(className: "unity-scroll-view__vertical-scroller")
                .Q("unity-dragger")
                .style.visibility = itemMax == 0
                    ? Visibility.Hidden
                    : Visibility.Visible;
        }

        DraggingContext StartDragging(VisualElement item, Vector2 worldPosition)
            => new DraggingContext(
                rootVisualElement,
                item as Image,
                //note: this even can come before the selection event of the
                //ListView. Reconstruct index by looking at target of the event.
                (int)item.layout.y / m_EnvironmentList.itemHeight,
                worldPosition);
        
        void EndDragging(DraggingContext context, Vector2 mouseWorldPosition)
        {
            Environment environment = LookDev.currentContext.environmentLibrary[context.draggedIndex];
            if (m_Views[(int)ViewIndex.First].ContainsPoint(mouseWorldPosition))
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingEnvironmentInViewInternal?.Invoke(environment, ViewCompositionIndex.Composite, mouseWorldPosition);
                else
                    OnChangingEnvironmentInViewInternal?.Invoke(environment, ViewCompositionIndex.First, mouseWorldPosition);
            }
            else
                OnChangingEnvironmentInViewInternal?.Invoke(environment, ViewCompositionIndex.Second, mouseWorldPosition);
        }

        class DraggingContext : IDisposable
        {
            const string k_CursorFollowerName = "cursorFollower";
            public readonly int draggedIndex;
            readonly Image cursorFollower;
            readonly Vector2 cursorOffset;
            readonly VisualElement windowContent;

            public DraggingContext(VisualElement windowContent, Image draggedElement, int draggedIndex, Vector2 worldPosition)
            {
                this.windowContent = windowContent;
                this.draggedIndex = draggedIndex;
                cursorFollower = new Image()
                {
                    name = k_CursorFollowerName,
                    image = draggedElement.image
                };
                cursorFollower.tintColor = new Color(1f, 1f, 1f, .5f);
                windowContent.Add(cursorFollower);
                cursorOffset = draggedElement.WorldToLocal(worldPosition);

                cursorFollower.style.position = Position.Absolute;
                UpdateCursorFollower(worldPosition);
            }

            public void UpdateCursorFollower(Vector2 mouseWorldPosition)
            {
                Vector2 windowLocalPosition = windowContent.WorldToLocal(mouseWorldPosition);
                cursorFollower.style.left = windowLocalPosition.x - cursorOffset.x;
                cursorFollower.style.top = windowLocalPosition.y - cursorOffset.y;
            }

            public void Dispose()
            {
                if (windowContent.Contains(cursorFollower))
                    windowContent.Remove(cursorFollower);
            }
        }

        class EnvironmentPreviewDragger : Manipulator
        {
            VisualElement m_DropArea;
            DisplayWindow m_Window;

            //Note: static as only one drag'n'drop at a time
            static DraggingContext s_Context;

            public EnvironmentPreviewDragger(DisplayWindow window, VisualElement dropArea)
            {
                m_Window = window;
                m_DropArea = dropArea;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            }

            void Release()
            {
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                s_Context.Dispose();
                target.ReleaseMouse();
                s_Context = null;
            }

            void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button == 0)
                {
                    target.CaptureMouse();
                    target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                    s_Context = m_Window.StartDragging(target, evt.mousePosition);
                    //do not stop event as we still need to propagate it to the ListView for selection
                }
            }

            void OnMouseUp(MouseUpEvent evt)
            {
                if (evt.button != 0)
                    return;
                if (m_DropArea.ContainsPoint(m_DropArea.WorldToLocal(Event.current.mousePosition)))
                {
                    m_Window.EndDragging(s_Context, evt.mousePosition);
                    evt.StopPropagation();
                }
                Release();
            }

            void OnMouseMove(MouseMoveEvent evt)
            {
                evt.StopPropagation();
                s_Context.UpdateCursorFollower(evt.mousePosition);
            }
        }

        Rect IViewDisplayer.GetRect(ViewCompositionIndex index)
        {
            switch (index)
            {
                case ViewCompositionIndex.First:
                case ViewCompositionIndex.Composite:    //display composition on first rect
                    return m_Views[(int)ViewIndex.First].contentRect;
                case ViewCompositionIndex.Second:
                    return m_Views[(int)ViewIndex.Second].contentRect;
                default:
                    throw new ArgumentException("Unknown ViewCompositionIndex: " + index);
            }
        }

        void IViewDisplayer.SetTexture(ViewCompositionIndex index, Texture texture)
        {
            switch (index)
            {
                case ViewCompositionIndex.First:
                case ViewCompositionIndex.Composite:    //display composition on first rect
                    if (m_Views[(int)ViewIndex.First].image != texture)
                        m_Views[(int)ViewIndex.First].image = texture;
                    break;
                case ViewCompositionIndex.Second:
                    if (m_Views[(int)ViewIndex.Second].image != texture)
                        m_Views[(int)ViewIndex.Second].image = texture;
                    break;
                default:
                    throw new ArgumentException("Unknown ViewCompositionIndex: " + index);
            }
        }

        void IViewDisplayer.Repaint() => Repaint();

        //[TODO]
        void IEnvironmentDisplayer.Repaint()
        {
            throw new NotImplementedException();
        }

        void ApplyLayout(Layout value)
        {
            switch (value)
            {
                case Layout.HorizontalSplit:
                case Layout.VerticalSplit:
                    if (!m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.AddToClassList(k_FirstViewClass);
                    if (!m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.AddToClassList(k_SecondViewsClass);
                    if (value == Layout.VerticalSplit)
                    {
                        m_ViewContainer.AddToClassList(k_VerticalViewsClass);
                        if (!m_ViewContainer.ClassListContains(k_VerticalViewsClass))
                            m_ViewContainer.AddToClassList(k_FirstViewClass);
                    }
                    break;
                case Layout.FullFirstView:
                case Layout.CustomSplit:       //display composition on first rect
                case Layout.CustomCircular:    //display composition on first rect
                    if (!m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.AddToClassList(k_FirstViewClass);
                    if (m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.RemoveFromClassList(k_SecondViewsClass);
                    break;
                case Layout.FullSecondView:
                    if (m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.RemoveFromClassList(k_FirstViewClass);
                    if (!m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.AddToClassList(k_SecondViewsClass);
                    break;
                default:
                    throw new ArgumentException("Unknown Layout");
            }

            //Add flex direction here
            if (value == Layout.VerticalSplit)
                m_ViewContainer.AddToClassList(k_VerticalViewsClass);
            else if (m_ViewContainer.ClassListContains(k_VerticalViewsClass))
                m_ViewContainer.RemoveFromClassList(k_VerticalViewsClass);
        }

        void ApplyEnvironmentToggling(bool open)
        {
            if (open)
            {
                if (!m_MainContainer.ClassListContains(k_ShowEnvironmentPanelClass))
                    m_MainContainer.AddToClassList(k_ShowEnvironmentPanelClass);
            }
            else
            {
                if (m_MainContainer.ClassListContains(k_ShowEnvironmentPanelClass))
                    m_MainContainer.RemoveFromClassList(k_ShowEnvironmentPanelClass);
            }
        }
    }
}
