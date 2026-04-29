"""Force-flip the surface/blend properties so the recovery billboard mats
actually render transparent (alpha=0 = invisible)."""
import os
import re
import glob

RECOV = r"C:\Dev\Forever engine\Assets\Procedural Worlds\_RecoveryMaterials\Trees"

def patch(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    original = content
    content = re.sub(r'(- _Surface:\s*)0\b', r'\g<1>1', content)
    content = re.sub(r'(- _DstBlend:\s*)0\b', r'\g<1>10', content)
    content = re.sub(r'(- _DstBlendAlpha:\s*)0\b', r'\g<1>10', content)
    content = re.sub(r'(- _SrcBlend:\s*)1\b', r'\g<1>5', content)
    content = re.sub(r'(- _ZWrite:\s*)1\b', r'\g<1>0', content)
    if content != original:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

for f in sorted(glob.glob(os.path.join(RECOV, '*Billboard*.mat'))):
    name = os.path.basename(f)
    print(('FIXED ' if patch(f) else 'NO-OP ') + name)
