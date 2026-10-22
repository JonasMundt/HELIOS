
export function hasOperatePermission(
  roles: readonly string[],
): boolean {
  return (
    roles.includes('Admin') ||
    roles.includes('Operator')
  )
}
