import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
r = d['data']['result']
print(r['Summary'])
for t in r['Results']:
    if t['Status'] != 'Passed':
        print('FAIL', t['FullName'])
        print('   ', (t.get('Message') or '').strip()[:600])
        st = (t.get('StackTrace') or '').strip().splitlines()
        for line in st[:4]: print('     ', line.strip()[:200])
