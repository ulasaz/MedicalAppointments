/** Badge colours per appointment status, shared by the appointments list and
 *  appointment detail pages so a status always reads the same colour everywhere. */
const STATUS_BADGE_CLASSES: Record<string, string> = {
  Pending: 'bg-amber-50 text-amber-700',
  Confirmed: 'bg-teal-50 text-teal-700',
  Completed: 'bg-fuchsia-50 text-fuchsia-700',
  Cancelled: 'bg-gray-100 text-gray-500',
  Rejected: 'bg-rose-50 text-rose-600',
};

export function statusBadgeClasses(status: string): string {
  return STATUS_BADGE_CLASSES[status] ?? 'bg-gray-100 text-gray-500';
}
