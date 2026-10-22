
import { describe, expect, it } from 'vitest'
import { hasOperatePermission } from './permissions'

describe('HELIOS Role Permissions', () => {
  it('allows Operators to control machines', () => {
    expect(
      hasOperatePermission(['Operator']),
    ).toBe(true)
  })

  it('allows Admins to control machines', () => {
    expect(
      hasOperatePermission(['Admin']),
    ).toBe(true)
  })

  it('denies machine control to Viewers', () => {
    expect(
      hasOperatePermission(['Viewer']),
    ).toBe(false)
  })

  it('denies machine control without a role', () => {
    expect(
      hasOperatePermission([]),
    ).toBe(false)
  })

  it('supports users with multiple roles', () => {
    expect(
      hasOperatePermission(['Viewer', 'Operator']),
    ).toBe(true)
  })
})
