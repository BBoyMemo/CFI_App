// Who may do what with a task, the same rules the server enforces.
//  - work:     add a progress / completion card while the task is open or in progress
//              (people it is assigned to, and managers);
//  - followUp: add a card once it is completed (anyone);
//  - edit / delete: managers; delete only while nobody has reported on it.
export function taskRules(task, user, isManager) {
  const assigned = task.assignees.some((a) => a.id === user.id);
  const completed = task.status === 'Completed';
  return {
    work: !completed && (isManager || assigned),
    followUp: completed,
    edit: isManager && !completed,
    delete: isManager && task.status === 'Open' && task.updates.length === 0,
  };
}
