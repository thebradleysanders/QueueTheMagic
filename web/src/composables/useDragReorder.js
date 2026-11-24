import { ref } from 'vue';

/** Returns a copy of the list with the item at `from` moved to `to`. */
export function moveItem(list, from, to) {
    if (from === to || from < 0 || to < 0 || from >= list.length || to >= list.length) return list;
    const copy = [...list];
    const [item] = copy.splice(from, 1);
    copy.splice(to, 0, item);
    return copy;
}

/**
 * Native drag-and-drop reordering for one list (desktop). Arrow buttons cover touch screens.
 * `onMove(from, to)` is called when an item is dropped on another position.
 */
export function useDragReorder(onMove) {
    const dragFrom = ref(null);
    const dragOver = ref(null);

    return {
        dragFrom,
        dragOver,
        start(i, e) {
            dragFrom.value = i;
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', String(i));
        },
        over(i, e) {
            if (dragFrom.value === null) return;
            e.preventDefault();
            dragOver.value = i;
        },
        drop(i, e) {
            e.preventDefault();
            const from = dragFrom.value;
            dragFrom.value = dragOver.value = null;
            if (from !== null) onMove(from, i);
        },
        end() {
            dragFrom.value = dragOver.value = null;
        }
    };
}
