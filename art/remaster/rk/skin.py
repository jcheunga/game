"""Skinning maths in numpy: bone deformation matrices, dual quaternions and their inverse.

Garments are grown around the body in its stance (rk/garb.py, rk/plate.py). To bind them, each vertex is
carried back to the rest pose through the inverse of its own blended dual-quaternion transform, so the
armature (which deforms with preserve-volume, i.e. dual quaternions) puts it exactly where it was built.
"""
import numpy as np


def mat_to_quat(R):
    """3x3 rotation -> quaternion (w, x, y, z)."""
    m = R
    tr = m[0, 0] + m[1, 1] + m[2, 2]
    if tr > 0:
        s = np.sqrt(tr + 1.0) * 2
        q = [0.25 * s, (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = np.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [(m[2, 1] - m[1, 2]) / s, 0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s]
    elif m[1, 1] > m[2, 2]:
        s = np.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 2] - m[2, 0]) / s, (m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s]
    else:
        s = np.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[1, 0] - m[0, 1]) / s, (m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s]
    q = np.array(q)
    return q / np.linalg.norm(q)


def qmul(a, b):
    aw, ax, ay, az = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    bw, bx, by, bz = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([aw * bw - ax * bx - ay * by - az * bz,
                     aw * bx + ax * bw + ay * bz - az * by,
                     aw * by - ax * bz + ay * bw + az * bx,
                     aw * bz + ax * by - ay * bx + az * bw], -1)


def qconj(q):
    return q * np.array([1, -1, -1, -1])


def quat_to_mats(q):
    w, x, y, z = q[..., 0], q[..., 1], q[..., 2], q[..., 3]
    R = np.empty(q.shape[:-1] + (3, 3))
    R[..., 0, 0] = 1 - 2 * (y * y + z * z)
    R[..., 0, 1] = 2 * (x * y - z * w)
    R[..., 0, 2] = 2 * (x * z + y * w)
    R[..., 1, 0] = 2 * (x * y + z * w)
    R[..., 1, 1] = 1 - 2 * (x * x + z * z)
    R[..., 1, 2] = 2 * (y * z - x * w)
    R[..., 2, 0] = 2 * (x * z - y * w)
    R[..., 2, 1] = 2 * (y * z + x * w)
    R[..., 2, 2] = 1 - 2 * (x * x + y * y)
    return R


def to_dq(M):
    M = np.asarray(M, dtype=np.float64)
    R = M[:3, :3]
    # strip any scale
    R = R / np.linalg.norm(R, axis=0, keepdims=True)
    q = mat_to_quat(R)
    t = np.array([0.0, M[0, 3], M[1, 3], M[2, 3]])
    qd = 0.5 * qmul(t, q)
    return q, qd


def blend(W, bones, mats):
    """Per-vertex blended dual quaternion -> (R (N,3,3), t (N,3)). W: (N, B) normalised weights."""
    Q = np.zeros((len(bones), 4))
    D = np.zeros((len(bones), 4))
    for j, b in enumerate(bones):
        M = mats.get(b)
        if M is None:
            Q[j] = (1, 0, 0, 0)
            continue
        Q[j], D[j] = to_dq(M)
    ref = Q[np.argmax(W, 1)]
    sign = np.sign((Q[None, :, :] * ref[:, None, :]).sum(-1))
    sign[sign == 0] = 1
    WS = W * sign
    bq = WS @ Q
    bd = WS @ D
    n = np.linalg.norm(bq, axis=1, keepdims=True)
    bq /= n
    bd /= n
    t = 2 * qmul(bd, qconj(bq))[:, 1:]
    return quat_to_mats(bq), t


def unpose(V, W, bones, mats):
    """Rest positions for posed points V under blended dual-quaternion skinning."""
    R, t = blend(W, bones, mats)
    return np.einsum('nji,nj->ni', R, V - t)


def pose_points(V, W, bones, mats):
    R, t = blend(W, bones, mats)
    return np.einsum('nij,nj->ni', R, V) + t
