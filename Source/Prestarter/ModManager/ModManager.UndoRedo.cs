namespace Prestarter;

public partial class ModManager
{
    private void Undo()
    {
        ClearSelection();

        // Currently up the undo stack
        if (undoneIndex > 0)
        {
            undoneIndex -= 1;
            active = undoStack[undoneIndex.Value];
        }

        // Not undoing
        if (undoneIndex is null && undoStack.Count > 0)
        {
            undoStack.Add(active);
            undoneIndex = undoStack.Count - 2;
            active = undoStack[undoneIndex.Value];
        }

        RecacheLists();
    }

    private void Redo()
    {
        ClearSelection();

        if (undoneIndex < undoStack.Count - 1)
        {
            undoneIndex += 1;
            active = undoStack[undoneIndex.Value];
        }

        RecacheLists();
    }

    private void PushUndo()
    {
        if (undoneIndex != null)
        {
            undoStack.RemoveRange(undoneIndex.Value, undoStack.Count - undoneIndex.Value);
            undoneIndex = null;
        }

        undoStack.Add(new UniqueList<string>(active));
    }
}
