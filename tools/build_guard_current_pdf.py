from pathlib import Path
from datetime import datetime, timezone, timedelta
import hashlib, json, re, subprocess, textwrap
from xml.sax.saxutils import escape
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, CondPageBreak, Table, TableStyle, Flowable, KeepTogether
from reportlab.lib.pagesizes import A4

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/K4RMA_Guard_Current_Patterns_Code_2026-10-31.pdf'
TMP = ROOT / 'tmp/pdfs/guard-current-2026-10-31'
OUT.parent.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)
STAMP = datetime.now(timezone(timedelta(hours=9))).strftime('%Y-%m-%d %H:%M KST')
BRANCH = subprocess.check_output(['git', 'branch', '--show-current'], cwd=ROOT, text=True).strip()
assert BRANCH == 'feat/minjae-boss-guard'
pdfmetrics.registerFont(TTFont('KR', 'C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('KRB', 'C:/Windows/Fonts/malgunbd.ttf'))
pdfmetrics.registerFont(TTFont('Mono', 'C:/Windows/Fonts/consola.ttf'))
pdfmetrics.registerFontFamily('KR', normal='KR', bold='KRB', italic='KR', boldItalic='KRB')
W, H = A4
M = 42
AW = W - 2*M - 12
INK = colors.HexColor('#17283D')
BLUE = colors.HexColor('#245A80')
GOLD = colors.HexColor('#C68B2C')
MUTED = colors.HexColor('#58677A')
PALE = colors.HexColor('#F2F6FA')
styles = {
    'title': ParagraphStyle('title', fontName='KRB', fontSize=21, leading=29, textColor=INK, spaceAfter=12, wordWrap='CJK'),
    'section': ParagraphStyle('section', fontName='KRB', fontSize=17, leading=24, textColor=INK, spaceAfter=12, keepWithNext=True, wordWrap='CJK'),
    'sub': ParagraphStyle('sub', fontName='KRB', fontSize=11.6, leading=17, textColor=BLUE, spaceBefore=13, spaceAfter=6, keepWithNext=True, wordWrap='CJK'),
    'body': ParagraphStyle('body', fontName='KR', fontSize=10.1, leading=16.4, textColor=INK, spaceAfter=7, wordWrap='CJK'),
    'small': ParagraphStyle('small', fontName='KR', fontSize=8.2, leading=12.5, textColor=MUTED, spaceAfter=6, wordWrap='CJK'),
    'cell': ParagraphStyle('cell', fontName='KR', fontSize=9.0, leading=14, textColor=INK, wordWrap='CJK'),
    'call': ParagraphStyle('call', fontName='KR', fontSize=10.1, leading=16.4, textColor=INK, wordWrap='CJK'),
}
story, excerpts, sources = [], [], {}
paths = {
    'ward': 'Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.Ward.cs',
    'shards': 'Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.WardShards.cs',
    'steps': 'Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.WardSteps.cs',
    'trial': 'Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.WardTrial.cs',
    'state': 'Assets/K4RMA/Runtime/Enemies/KarmaEnemy.StateMachine.cs',
    'enemy': 'Assets/K4RMA/Runtime/Enemies/KarmaEnemy.cs',
    'combat': 'Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Combat.cs',
    'present': 'Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs',
    'catalog': 'Assets/K4RMA/Core/KarmaPatternCatalog.cs',
    'rules': 'Assets/K4RMA/Core/KarmaBossRules.cs',
    'essences': 'Assets/K4RMA/Runtime/Abilities/KarmaEssences.cs',
    'player': 'Assets/K4RMA/Runtime/Player/KarmaPlayer.Status.cs',
    'playerCombat': 'Assets/K4RMA/Runtime/Player/KarmaPlayer.Combat.cs',
    'dome': 'Assets/K4RMA/Runtime/Presentation/KarmaGuardDome.cs',
    'ground': 'Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.GroundStrike.cs',
    'space': 'Assets/K4RMA/Runtime/World/KarmaSpace.cs',
    'config': 'Assets/K4RMA/Settings/KarmaConfig.asset',
}
def source(key):
    path = paths[key]
    if path not in sources:
        raw = (ROOT/path).read_bytes()
        sources[path] = {'sha256': hashlib.sha256(raw).hexdigest(), 'lines': raw.decode('utf-8-sig').splitlines()}
    return sources[path]['lines']

def extract(key, marker, end=None):
    lines = source(key)
    start = next(i for i,s in enumerate(lines) if marker in s)
    if end is not None:
        before=end.startswith('before:')
        target=end[7:] if before else end
        stop = next(i for i in range(start, len(lines)) if target in lines[i]) + (0 if before else 1)
    else:
        began, depth, stop = False, 0, start
        for i in range(start, len(lines)):
            # Selected source methods/classes have no brace characters in their string literals.
            depth += lines[i].count('{') - lines[i].count('}')
            began |= '{' in lines[i]
            if began and depth == 0:
                stop = i+1
                break
        assert stop > start, marker
    raw = lines[start:stop]
    record = {'id': f'C{len(excerpts)+1:02d}', 'path': paths[key], 'start': start+1, 'end': stop, 'raw_lines': raw}
    excerpts.append(record)
    return record

def runs(s):
    return [('Mono' if all(ord(c)<128 for c in part) else 'KR', part) for part in re.findall(r'[\x00-\x7f]+|[^\x00-\x7f]+', s)]
def measure(s, size=8.5):
    return sum(pdfmetrics.stringWidth(t, f, size) for f,t in runs(s))
def wrap_code(s, limit):
    if not s: return ['']
    chunks = []
    while measure(s) > limit:
        n = 1
        while n < len(s) and measure(s[:n+1]) <= limit: n += 1
        cut = s.rfind(' ', 0, n)
        if cut < n*.55: cut = n
        else: cut += 1
        chunks.append(s[:cut]); s = s[cut:]
    chunks.append(s)
    return chunks

class CodePanel(Flowable):
    def __init__(self, rec, label, rows=None, continued=False):
        super().__init__(); self.rec=rec; self.label=label; self.rows=rows; self.continued=continued
        self.spaceBefore=6; self.spaceAfter=9
    def wrap(self, availWidth, availHeight):
        self.width=min(availWidth,AW)
        if self.rows is None:
            raw = self.rec['raw_lines']
            indent = min((len(s)-len(s.lstrip()) for s in raw if s.strip()), default=0)
            self.rows=[]
            for num, line in enumerate(raw, self.rec['start']):
                for j, piece in enumerate(wrap_code(line[indent:], self.width-57)):
                    self.rows.append((str(num) if j == 0 else '', piece))
        self.height=44+len(self.rows)*11.7+10
        return self.width, self.height
    def split(self, availWidth, availHeight):
        self.wrap(availWidth, availHeight)
        n=int((availHeight-54)//11.7)
        if 0 < len(self.rows)-n < 8: n=len(self.rows)-8
        if n < 5: return []
        if n >= len(self.rows): return [self]
        return [CodePanel(self.rec,self.label,self.rows[:n],self.continued), CodePanel(self.rec,self.label,self.rows[n:],True)]
    def draw(self):
        c=self.canv; w=self.width; h=self.height
        c.setFillColor(PALE); c.roundRect(0,0,w,h,5,fill=1,stroke=0)
        c.setFillColor(GOLD); c.rect(0,0,3,h,fill=1,stroke=0)
        c.setFont('KRB',8.1); c.setFillColor(BLUE)
        suffix=' / 계속' if self.continued else ''
        label=f"{self.rec['id']}  {self.label}{suffix}"
        assert pdfmetrics.stringWidth(label,'KRB',8.1) < w-24, label
        c.drawString(12,h-14,label)
        c.setFont('KR',7.4); c.setFillColor(MUTED)
        c.drawString(12,h-27,f"원본 {Path(self.rec['path']).name} | L{self.rec['start']}-L{self.rec['end']}")
        y=h-45
        for number,s in self.rows:
            c.setFont('Mono',7.3); c.setFillColor(MUTED); c.drawRightString(35,y,number)
            x=45; c.setFillColor(colors.HexColor('#4E755D') if s.lstrip().startswith('//') else INK)
            for f,t in runs(s):
                c.setFont(f,8.5); c.drawString(x,y,t); x+=pdfmetrics.stringWidth(t,f,8.5)
            assert x <= w-10+0.1, (self.label,s,x,w)
            y-=11.7

class Sequence(Flowable):
    def __init__(self, labels):
        super().__init__(); self.labels=labels
    def wrap(self, aw, ah): self.width=aw; self.height=64; return aw,64
    def draw(self):
        c=self.canv; n=len(self.labels); gap=16; bw=(self.width-gap*(n-1))/n
        for i,label in enumerate(self.labels):
            x=i*(bw+gap); c.setFillColor(PALE); c.setStrokeColor(colors.HexColor('#D9E3ED'))
            c.roundRect(x,10,bw,45,5,fill=1,stroke=1)
            ls=label.split('\n'); c.setFillColor(INK); c.setFont('KRB',9)
            for j,line in enumerate(ls): c.drawCentredString(x+bw/2,37-j*14,line)
            if i<n-1:
                c.setStrokeColor(GOLD); c.line(x+bw+3,32,x+bw+gap-4,32)
                p=c.beginPath(); p.moveTo(x+bw+gap-7,35); p.lineTo(x+bw+gap-3,32); p.lineTo(x+bw+gap-7,29); c.drawPath(p)

class Doc(SimpleDocTemplate):
    def afterFlowable(self, flow):
        if isinstance(flow,Paragraph) and flow.style.name == 'section':
            title=flow.getPlainText(); key='sec'+str(self.page)+'_'+str(len(title))
            self.canv.bookmarkPage(key); self.canv.addOutlineEntry(title,key,0,False)

def para(s, kind='body'):
    story.append(Paragraph(s, styles[kind]))
def heading(s): para(s,'sub')
def chapter(s):
    if story:
        story.append(CondPageBreak(240))
        story.append(Spacer(1,16))
    para(s,'section')
def table(headers, rows, widths):
    data=[[Paragraph(escape(str(x)),styles['cell']) for x in row] for row in [headers]+rows]
    t=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#E6EEF5')),('VALIGN',(0,0),(-1,-1),'TOP'),
        ('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),8),
        ('BOTTOMPADDING',(0,0),(-1,-1),8),('LINEBELOW',(0,0),(-1,0),0.8,BLUE),
        ('LINEBELOW',(0,1),(-1,-1),0.35,colors.HexColor('#D8E1EA'))]))
    if len(rows) <= 8:
        prefix=[]
        if story and isinstance(story[-1],Paragraph) and story[-1].style.name == 'sub': prefix=[story.pop()]
        story.append(KeepTogether(prefix+[t,Spacer(1,8)]))
    else: story.append(t); story.append(Spacer(1,8))
def call(s):
    t=Table([[Paragraph(s,styles['call'])]],colWidths=[AW]); t.setStyle(TableStyle([
        ('BACKGROUND',(0,0),(-1,-1),colors.HexColor('#FFF5E3')),('BOX',(0,0),(-1,-1),0.5,colors.HexColor('#E8CB99')),
        ('LEFTPADDING',(0,0),(-1,-1),12),('RIGHTPADDING',(0,0),(-1,-1),12),('TOPPADDING',(0,0),(-1,-1),10),('BOTTOMPADDING',(0,0),(-1,-1),10)]))
    story.append(t); story.append(Spacer(1,9))
def code(key, marker, label, end=None):
    rec=extract(key,marker,end)
    story.append(CodePanel(rec,label))
    return rec
def explain(*items):
    for i,item in enumerate(items,1): para(f'<b>{i}.</b> {item}')

cfg={}
for s in source('config'):
    m=re.match(r'\s+(\w+):\s+([0-9.]+)$',s)
    if m: cfg[m[1]]=float(m[2])
assert cfg['fragmentChaseSpeed']==30 and cfg['fragmentReturnSeconds']==.4
assert '                BreakAutoShield();' in source('trial')

para('방어형 보스 패턴<br/>현재 동작과 원본 코드 해설','title')
para(f'{STAMP} | {BRANCH} 작업 트리 기준 | 미커밋 변경 포함','small')
para('팀 회의에서 패턴의 의도, 플레이어의 대응, 실제 판정과 코드를 함께 설명하기 위한 자료다. 별도 표지 없이 현재 규칙부터 시작한다. 수치의 기준은 현재 설정 에셋이며, 코드는 문서 제작 시점의 원본에서 직접 추출했다.')
table(['패턴','핵심 동작','플레이어와의 관계'],[
    ['GD01','지속 방어막','피해를 30%만 받는다. GD02 내려베기 패링 또는 GD05 성공으로 해제한다.'],
    ['GD02','위에서 아래로 강공격','붉은 빛 0.5초 안의 K 입력을 저장한다. 내려베기 접촉 시 패링이 성립한다.'],
    ['GD03','큰 방패 투척 → 파편 추적·복귀','붉은 파편은 피해+기절, 식은 복귀 파편은 피해만 준다. 멀리 있으면 추적 시간을 연장한다.'],
    ['GD04','한쪽으로 계단식 지면 상승','뒤로 피했을 때 GD01이 남아 있으면 반대 방향 GD02가 이어진다. 벽에서는 배치를 축소한다.'],
    ['GD05','노란 반원 결계 생존 시험','기본 7회, 실제 피격 1회까지 성공. 성공하면 GD01을 해제하고 4초 공격 기회를 준다.'],
],[52,130,AW-182])
heading('현재 버전에서 반드시 구분할 규칙')
para('GD01은 시간이 지나거나 공격을 회피했다고 사라지지 않는다. GD05의 보스 완전 무적과 GD01의 70% 피해 감소는 별도 상태다. GD05 성공은 두 상태를 모두 끝내고, 실패는 완전 무적만 끝낸다.')
call('<b>설명 순서:</b> 해금·실행 구조 → GD01 → GD02의 입력 저장과 실제 패링 → GD03 → GD04 연계 → GD05 성공·실패 → 공통 판정·정리. PDF 책갈피로 각 장에 바로 이동할 수 있다.')

chapter('1. 해금·패턴 선택·일반 공격')
table(['보스 단계','반납한 능력 순서','방어형 패턴'],[
    ['2스테이지','방어','GD01 + GD02 + 일반 연속 베기'],
    ['3스테이지','방어 → 검기','GD01 + GD02 + GD03 + 일반 연속 베기'],
    ['3스테이지','방어 → 상승베기','GD01 + GD02 + GD04 + 일반 연속 베기'],
    ['최종 스테이지','방어 → 검기 → 상승 / 방어 → 상승 → 검기','GD01~GD05 + 일반 연속 베기. 최종 외형은 두 순서 공통.'],
],[77,180,AW-257])
para('코드에서 Flame은 검기, Dash는 상승베기, Ward는 방어 능력이다. 반납 순서의 첫 능력이 보스의 주 형태를 결정한다. GD01은 공격 목록을 차례대로 소비하는 패턴이 아니라 별도로 유지하는 방어 상태다.')
code('catalog','                case Essence.Ward:','BuildCycle의 방어형 분기','                    break;')
code('catalog','public static bool CanSelect(','다음 공격을 선택할 수 있는 조건')
explain('GD02는 방어막이 있고 정규화 거리 4 이하일 때 선택한다. 공간 배율 2를 적용하면 게임 거리 8이다. 공격 중심과 너비도 GuardHeavyReach=4를 공유한다.',
    'GD04는 보스가 벽에서 5보다 멀리 있어야 선택 가능하다. 시전 내부에서는 실제 플레이어 방향의 남은 공간에 맞춰 계단을 배치한다.',
    '궁극기 GD05는 쿨다운이 끝났고 직전 공격이 궁극기가 아닐 때 선택한다. 기본 쿨다운은 20초이며, 다음 발동 가능 시각은 시전 시작에 갱신한다.')
code('state','void BeginAttack()','공격 목록 순회와 시전 시작')
para('cycleIndex를 증가시키며 목록을 순회하고 조건을 만족하는 공격을 고른다. 조건을 만족하지 못한 공격은 건너뛴다. 실행 중에는 새 공격을 중첩 선택하지 않는다. GD01을 유지한다고 GD02만 반복하는 구조가 아니다.')
heading('일반 연속 베기도 공격 순환에 들어간다')
code('ground','IEnumerator GroundStrike()','일반 연속 베기 전체')
explain('기본 2타, 체력 50% 이하의 강화 상태에서는 3타다. 각 타 사이에 플레이어 위치를 다시 읽지만, 한 타의 주황색 전조가 시작된 뒤에는 그 타의 범위가 고정된다.',
    '보스가 짧게 접근한 뒤 공격한다. 첫 타가 끝났다고 바로 반격하기보다 마지막 마무리 타와 후딜레이를 구분한다.')

chapter('2. 코루틴과 시간·상태를 읽는 법')
para('<b>코루틴</b>은 함수를 여러 프레임에 나눠 이어 실행하는 Unity 방식이다. 별도 스레드가 아니다. IEnumerator 함수에서 yield return null을 만나면 다음 프레임까지 멈췄다가 이어진다. StartCoroutine은 이 실행을 시작한다.')
code('state','IEnumerator Wait(float seconds)','일시정지를 고려한 대기')
explain('elapsed는 지금까지 지난 게임 시간이다. 매 프레임 기다리되 일시정지 중에는 누적하지 않아 전조·공격·정지 시간이 보존된다.',
    'yield return 다른 코루틴은 그 실행이 끝날 때까지 현재 순서를 대기시킨다. GD03의 여러 파편과 GD04의 지면 상승은 별도로 움직이면서 상위 패턴이 완료를 기다린다.')
story.append(Sequence(['Approach\n접근·선택','Telegraph\n공격 예고','Attack\n피해 판정','Recover\n후딜레이']))
para('GD02 내려베기 패링이 성공하면 일반 흐름을 중단하고 Stunned로 들어간다. 2초가 지나면 Approach로 복귀한다. GD05 성공의 4초 공격 기회는 Stunned가 아니라 Recover 상태다.')
code('enemy','public bool Enraged','강화 상태·속도 배율·전조 시간','float Warning(float seconds)')
code('state','IEnumerator Recovery(','일반 후딜레이와 고정 보상 시간')
table(['구분','일반 상태','강화 상태'],[
    ['강화 기준','보스 HP 50% 초과','보스 HP 50% 이하'],
    ['Tempo','1','0.82'],
    ['GD04 전조','0.8 × 0.9 = 0.72초','0.8 × 0.9 × 0.82 = 약 0.59초'],
    ['GD03 파편 조준','0.5 × 0.9 = 0.45초','약 0.369초'],
    ['GD02 붉은 빛','0.5초 고정','0.5초 고정'],
    ['GD05 성공 보상','4초 고정','4초 고정'],
],[144,180,AW-324])
para('Warning은 최소 0.28초를 보장한다. 일반 Recovery는 회복 배율 0.72와 Tempo를 적용하지만, GD05 성공은 scaleDuration:false여서 4초가 줄어들지 않는다. 거리·속도 값은 KarmaSpace.Units에서 2배가 되며 시간·피해는 공간 배율로 확대하지 않는다.')
code('space','        public const float Scale','게임 공간 배율과 그림 배율','        public static Vector2 Units')
para('Units는 거리·속도를 실제 게임 공간으로 변환하는 함수다. GuardianArtScale=1.7은 보스 그림의 배율이므로 공간 배율 2와 혼동하지 않는다. GD02의 선택 거리 4는 Units를 적용한 실제 거리 8에 대응한다.')

chapter('3. GD01 - 지속 방어막과 해제·재생성')
para('방어형이 된 스테이지는 시작부터 GD01을 켠다. 보호막이 켜져 있으면 플레이어 공격 피해의 30%만 보스 체력에 적용한다. 기본 검격 60은 18이 된다. 방어막 자체에 5초 같은 만료 시간은 없다.')
story.append(Sequence(['스테이지 시작\nGD01 활성','해제 조건\nGD02 패링 / GD05 성공','8초 대기\n공격·회복 종료','0.8초 예고\n재생성']))
code('ward','void UpdateAutoShield()','방어막 갱신 전체')
code('ward','void RefreshAutoShield()','방어막 켜기')
code('ward','void BreakAutoShield()','방어막 해제와 재생성 예약')
explain('autoShield가 실제 GD01 활성 여부다. BreakAutoShield는 이 값을 false로 바꾸고 생성 예고도 지운 뒤, nextShield에 현재 전투 시간+8초를 저장한다.',
    '8초가 지났다고 어떤 상황에서나 즉시 켜지는 것은 아니다. 결계 시험·경직·후딜레이·정면 방어 중에는 갱신을 보류하며 새 공격이 실행 중이면 재생 예고를 시작하지 않는다.',
    '기본 0.8초 예고 동안 방어막 표시가 깜빡이고, 예고가 끝나면 RefreshAutoShield가 실행된다. 따라서 실제 재생성 시점은 8.8초보다 늦어질 수 있다.')
code('combat','public void TakeDamage(float amount, Vector2 source)','보스 피해 감소와 완전 무적')
call('<b>코드 리뷰 핵심:</b> fullShield는 피해를 아예 받지 않는 완전 무적이고, autoShield는 피해를 0.3배로 만드는 GD01이다. frontalGuard와 autoShield가 함께 있어도 이 함수는 감소를 한 번 적용한다. 0.3 × 0.3으로 중첩하지 않는다.')
para('GD02를 피하거나 맞는 것, GD03·GD04의 종료, GD05 진입·실패는 GD01을 해제하지 않는다. 현재 해제 경로는 GD02 내려베기 패링과 GD05 생존 시험 성공 두 가지다. 사망·전투 종료에서는 별도로 정리한다.')

chapter('4. GD02 - 강공격과 0.5초 패링 입력 저장')
para('모든 방어형 형태에서 사용하는 큰 내려베기다. 일반 선택 거리는 게임 거리 8 이내이며, 실제 사각 판정은 보스 앞쪽으로 너비 8·높이 8.8이다. 검기 계승 여부와 체력 조건에 따라 후속 지면 충격파가 추가된다.')
story.append(Sequence(['주황 전조\n공격 범위 고정','붉은 빛 0.5초\nK 입력 저장','내려베기 접촉\n패링 판정','성공: GD01 해제\n보스 2초 경직']))
code('ward','IEnumerator HeavyStrike(','강공격 전체: 전조부터 후딜레이까지')
heading('전체 함수의 실행 순서')
explain('center와 size를 시전 시작에 계산해 이번 공격 범위를 고정한다. 일반 상태의 전체 준비 시간은 Warning(0.9)=0.81초이며, 마지막 0.5초에 붉은 빛이 나온다. 강화 상태도 붉은 빛 자체는 0.5초다.',
    '큐가 떠 있는 동안 매 프레임 무기 손 위치를 따라간다. 불빛을 끄고 animator.SwingHeavyStrike를 호출한 뒤 Attack 상태로 전환한다.',
    '공격 접촉 기록에 breaksShieldOnParry:true를 넣는다. 같은 타격을 즉시 검사하고 0.18초 유효 공격도 생성한다. 하나의 기록을 공유하므로 같은 내려베기가 두 번 맞지 않는다.',
    '실제 피격은 기본 보스 피해의 1.5배이고 플레이어 기절은 1초다. 패링·무적·방어로 체력이 줄지 않으면 피격 기절을 적용하지 않는다.',
    '검기 잔재(Flame)가 있고 보스 HP가 50% 이하이면 추가 전조 후 낮은 충격파를 낸다. 기본 방어형과 상승베기만 계승한 형태에는 생기지 않는다. 두 최종 계승 순서에는 모두 생긴다.')
heading('K를 일찍 눌러도 입력이 사라지지 않는 이유')
code('enemy','public bool HeavyParryWindowOpen','현재 입력 창을 허용하는 조건')
code('enemy','public bool TryQueueHeavyParry()','패링 입력 예약')
para('TryQueueHeavyParry가 heavyParryQueued=true로 입력을 저장한다. 실제 성공 판정은 이 함수가 아니라 내려베기 접촉 처리에서 이뤄진다.')
code('essences','public void TryCounter()','실제 K 입력 처리')
explain('일반 반격의 유효 시간은 0.18초, 쿨다운은 1.1초다. GD02 큐 안에서 K가 예약되면 일반 반격 쿨다운 때문에 입력을 버리지 않는다.',
    '붉은 빛 안에서 K를 눌렀다고 그 자리에서 즉시 보스를 경직시키지는 않는다. 실제 내려베기가 플레이어에게 접촉할 때 저장된 입력을 다시 반격 상태로 연결한다. 범위를 벗어나 공격 자체가 닿지 않으면 패링 횟수도 증가하지 않는다.')
code('playerCombat','public void BeginCounter()','플레이어의 짧은 반격 판정 열기')
code('playerCombat','public void TakeDamage(float damage)','플레이어 피해 처리 전체: 반격 성공과 실제 피격')
explain('피격 무적을 먼저 확인하고, 살아 있는 반격 판정을 ConsumeCounter로 소비하면 ParryCount를 증가시킨다. 이 증가가 보스 쪽의 실제 패링 성공 증거가 된다.',
    '반격 성공 시 짧은 무적과 반격 공격을 제공하고 일반 피해 처리를 종료한다. 반격이 아니라면 방어 상태를 확인하고, 보호되지 않은 경우에만 체력을 줄인다.',
    '이 함수의 반격 피해는 보스가 GD01을 해제하기 전에 발생할 수 있어 현재 GD01 감소가 적용될 수 있다. GD01 해제 후의 후속 공격 피해와 반격 순간의 피해를 구분해서 설명한다.')
code('present','public void TryAttackHit(','공통 접촉 처리와 GD02 전용 예약 소비')
code('state','            if (pendingParry)','패링 후 패턴 중단·방어막 해제·경직','before:            UpdateAutoShield();')
para('플레이어 TakeDamage 전후의 ParryCount를 비교해서 실제 패링 성공을 확인한다. 내려베기 접촉에만 pendingParry를 예약하고 다음 Update에서 공격 오브젝트를 지운 뒤 GD01을 해제한다. 후속 충격파는 일반 반격이 가능해도 이 플래그를 만들지 않아 GD01을 깨거나 보스를 경직시키지 않는다.')
call('<b>발표 문장:</b> “0.5초는 버튼을 받아 두는 시간이고, 실제 패링은 내려베기와 플레이어가 닿을 때 결정됩니다. 입력 예약과 타격 성공 판정을 분리했습니다.”')
para('경직 표시는 각 형태의 전용 Stagger 스프라이트를 사용한다. C 자세에 작은 A 번개를 결합하고 기존 색상에 0.82를 곱한다. 이는 표시 처리이며, 실제 2초 경직 상태와 타격 판정은 위 로직이 담당한다.')

chapter('5. GD03 - 투척·분열·원거리 추적·고정 복귀')
para('검기를 계승한 방어형과 최종형에서 사용한다. 손 부근의 큰 방패가 먼저 고정된 목표를 향해 날아가고, 실제 도착 위치에서 같은 파편들이 퍼져 순서대로 추적한다. 네 파편은 서로 다른 시간에 출발하고 끝날 수 있다.')
story.append(Sequence(['큰 방패 투척\n0.5초 / 피해만','분열·확산\n0.28초 / 무해','붉은 파편 추적\n피해 + 1초 기절','식은 파편 복귀\n0.4초 / 피해만']))
code('shards','IEnumerator SplitShield()','큰 방패에서 네 파편으로 이어지는 전체 순서')
explain('투척 목표는 최초 플레이어 X 위치를 고정하고 경기장 안으로 보정한다. 큰 방패의 비행은 Lerp를 사용해 0.5초 동안 출발점에서 목표점까지 진행한다.',
    '도착한 shield.transform.position을 impact로 다시 읽는다. 네 파편을 이 한 점에 만들고 155도부터 25도 방향으로 0.28초 동안 퍼뜨린다. 큰 방패는 첫 0.12초에 사라지므로 투척과 분열 사이에 위치 단절이 생기지 않는다.',
    '각 파편의 조준 코루틴을 기본 0.3초 간격으로 시작한다. 전부 발사한 다음 flights의 모든 코루틴 완료를 기다린다. 마지막에 발사한 파편만 기다리면 먼저 발사한 원거리 파편이 중간에 삭제될 수 있어 전체를 기다린다.')
code('shards','IEnumerator ShieldFragment(','파편 한 개의 조준·추적·정지·복귀 전체')
heading('원거리 추적 시간은 어떻게 정하는가')
para('현재 설정에서 파편 속도는 30, 플레이어 기본 이동 속도는 14다. 같은 방향으로 달아날 때 거리를 줄이는 속도를 closingSpeed = 30 - 14 = 16으로 추정한다. 조준이 끝난 시점의 거리를 이 값으로 나누고 0.15초 여유를 더한 뒤 0.65~5초로 제한한다.')
table(['조준 종료 시 거리','계산한 추적 시간 상한','해석'],[
    ['4','0.65초','근거리는 기존 기본 추적 시간을 유지'],
    ['16','1.15초','멀어진 플레이어를 따라갈 시간 확보'],
    ['40','2.65초','정지 플레이어는 더 빨리 접촉 가능'],
    ['70','4.525초','긴 거리도 허용하되 최대 5초로 제한'],
],[100,150,AW-250])
explain('이 값은 무조건 그만큼 쫓는 고정 시간이 아니라 추적 예산이다. 최소 0.65초가 지났고 outgoing.Consumed가 true이면 멈춰 복귀 준비로 넘어간다. Consumed는 피해뿐 아니라 패링·무적 접촉 등으로 접촉 시도가 소진된 경우도 포함한다.',
    '추적 중 target은 매 프레임 플레이어 위치로 바뀐다. MoveTowards는 한 프레임에 speed × dt만큼 이동하므로 프레임 속도에 크게 의존하지 않고 속도 30을 유지한다.',
    'dt는 남은 추적 시간보다 커지지 않게 제한한다. 타이머 종료가 한 프레임 넘어가도 이동량이 과해지는 것을 막는다.')
heading('복귀는 다시 추적하는 동작이 아니다')
para('정지 순간 stopped와 home을 한 번 저장한다. 0.18초 예고 후 Lerp(stopped, home, elapsed / returnSeconds)로 0.4초 동안 돌아온다. 그 뒤 플레이어가 움직여도 이 두 점은 바뀌지 않는다. 같은 거리에서 기존 0.6초 복귀보다 1.5배 빠르며, 거리가 길수록 실제 복귀 속도도 커진다.')
para('추적과 복귀는 각각 outgoing, returning이라는 별도 접촉 기록을 사용한다. 나갈 때 맞았다고 돌아올 때 판정까지 소모되지는 않는다. 추적은 피해 배율 0.8과 기절 1초, 복귀는 피해 배율 0.8과 기절 0을 전달한다. 피격 무적이 남아 있으면 실제 추가 피해는 막힐 수 있다.')
code('shards','static void SetFragmentHeat(','붉은 추적 / 식은 복귀 외형 전환')
call('<b>발표 문장:</b> “갈 때는 실시간 추적이고, 돌아올 때는 정지한 위치와 보스를 잇는 고정 경로입니다. 먼 거리 때문에 파편별 종료 순서가 달라져도 모두 끝날 때까지 패턴을 유지합니다.”')
para('파편 수 4개, 조준 기본 0.5초에 전조 배율 적용, 발사 간격 0.3초, 추적 0.65~5초, 정지 0.18초, 복귀 0.4초다. 무한 추적은 하지 않는다. 패턴 감시 타이머도 GD03에 최소 12초를 확보해 긴 추적을 중간에 자르지 않는다.')

chapter('6. GD04 - 벽을 고려한 계단과 후방 GD02 연계')
para('상승베기를 계승한 방어형과 최종형에서 사용한다. 시전 시작에 플레이어가 있는 방향을 정하고, 그 방향으로 낮은 지면부터 차례대로 올린다. 보스가 GD02로 돌아서도 이미 정한 지면의 방향은 바뀌지 않는다.')
code('steps','IEnumerator RisingBarrier()','지면 배치와 후방 강공격 연계 전체')
heading('벽 근처 배치를 줄이는 계산')
explain('room은 보스부터 진행 방향의 경기장 끝까지 남은 거리에서 0.3을 뺀 값이다. gap은 첫 계단 앞쪽 여유이며 기본 2.4보다 커지지 않는다.',
    '기본 폭 3.3과 간격 폭×0.92로 들어갈 계단 수를 계산한다. 우선 3개 이상을 시도하고, 폭이 1.5보다 좁아지면 계단 수를 줄여 발을 디딜 폭을 확보한다.',
    '중앙의 기본 배치는 6개다. 각 높이는 2.2, 3.4, 4.6, 5.8, 7.0, 8.2다. 벽 가까이는 2~3개 등으로 줄어들 수 있지만, 플레이어 반대 방향으로 공격을 뒤집지는 않는다.')
heading('뒤로 피했는지를 판정하는 signedDistance')
para('signedDistance = (플레이어 X - 보스 X) × terrainDirection이다. 지면 방향에 있으면 양수, 보스 뒤로 넘어가면 음수다. GD01이 있고, 앞에 있었던 기록이 있고, signedDistance가 -0.7보다 작으면서 뒤쪽 거리 8.7 이내이면 GD02를 한 번 이어 쓴다.')
story.append(Sequence(['플레이어가 지면 쪽\nGD01 유지','보스 뒤로 이동\n거리 부호: 음수','보스만 반대로 회전\n지면은 원래 방향','붉은 빛에 K\nGD01 해제·지형 정리']))
para('이 함수의 yield return HeavyStrike(wave)는 지면 상승 코루틴을 함께 넘긴다. 패링 실패 시에도 이미 시작한 지면 생성이 끝나기 전에 후딜레이가 지형을 지우지 않도록 GD02 쪽에서 wave를 기다린다. 성공 시에는 InterruptPattern이 모든 코루틴과 패턴 오브젝트를 정리한다.')
code('steps','IEnumerator RiseBarrierWave(','계단을 기본 0.22초 간격으로 상승')
code('steps','IEnumerator RiseBarrierStep(','한 계단의 성장·피해·물리 지형 전환')
explain('시각 지면은 0.18초에 걸쳐 바닥에서 자란다. 성장이 끝나면 0.25초 동안 한 번의 피해 판정을 두고, 이후 무해한 물리 지형으로 전환한다.',
    '완성 시 플레이어가 내부에 있으면 ResolveBarrier로 윗면에 올려 끼임을 해소한다. BoxCollider2D와 KarmaSurface가 붙어 발판으로 쓸 수 있다.',
    '후방 연계가 없으면 마지막 계단 완료 뒤 0.6초 유지하고 회복한다. 후방 연계가 있으면 내려베기·필요 시 충격파가 끝난 뒤 정리하거나, 패링 성공 즉시 정리한다.')
call('<b>설명할 때 구분:</b> GD01의 피해 감소와 GD04의 정면 방어는 별도 상태다. 뒤로 가면 정면 방어는 우회하지만 GD01은 아직 남는다. 이어지는 GD02를 패링해야 GD01까지 해제된다.')

chapter('7. GD05 - 결계 시험의 진행과 점수')
para('두 능력을 모두 계승한 최종 방어형의 생존 패턴이다. 준비 시작부터 시험·실패 과부하·폭발이 끝날 때까지 보스가 완전 무적이다. 결계는 반지름 20, 지름 40의 노란 반원이며 중심은 x=20이다. 좌우 제한은 1.2~38.8, 입장 시 위치 보정은 3.2~36.8 안쪽으로 이뤄진다.')
table(['설정','현재 값','뜻'],[
    ['trialAttackCount','7회','공격 묶음이 7번 끝나면 결과를 결정'],
    ['trialAllowedHits','1회','0~1회 실제 피해는 성공, 2회 이상은 실패'],
    ['trialAttackGap','0.65초','묶음 사이 대기. Tempo 적용, 최소 0.4초'],
    ['trialStunSeconds','4초','성공 후 공격 기회. 강화 상태도 고정'],
    ['trialExplosionMultiplier','2.5배','실패 시 룸 폭발 피해'],
],[150,90,AW-240])
code('trial','IEnumerator SurvivalTrial()','결계 준비부터 성공·실패 처리까지 전체')
heading('판정 결과를 먼저 저장해야 하는 이유')
code('rules','public sealed class KarmaTrialState','시험 점수 상태 전체')
explain('Finished는 공격 묶음이 모두 끝났는지를 뜻하고, Passed는 끝났으며 실제 피격 수가 허용치 이하인지를 뜻한다. 중간에 2번 맞아도 바로 종료하지 않고 예정된 시험을 진행한 뒤 실패 폭발로 넘어간다.',
    'bool passed = trial.Passed로 결과를 저장한 다음 trial.Clear를 호출한다. Clear는 점수와 Total까지 초기화하므로, Clear 뒤 Passed를 읽으면 원래 결과를 잃는다.',
    'trial.Hit는 공통 타격 함수에서 플레이어 체력이 실제 줄었을 때만 호출한다. 패링·방어·무적으로 체력이 줄지 않은 접촉은 시험 피격 수에 더하지 않는다.')
heading('7회가 모두 다른 7가지 공격은 아니다')
para('i % 5로 다섯 종류를 순환한다. 기본 7회 순서는 낮은 파동 → 낙하 → 협공 → 기둥 → 높낮이 연속 → 반대쪽 낮은 파동 → 낙하다. 같은 공격 묶음의 여러 탄은 접촉 기록 하나를 공유한다.')
table(['종류','한 번의 생성 수','플레이어 대응'],[
    ['낮은 파동','2개','낮은 파동 묶음을 점프하거나 반격'],
    ['봉인 낙하','5개','조준이 고정된 뒤 옆 빈 공간으로 이동'],
    ['양쪽 협공','4개','양방향 낮은 파동의 만나는 시점을 보고 점프·반격'],
    ['바닥 기둥','5개','예고된 기둥 사이 빈 칸으로 이동'],
    ['높낮이 연속','4개','높은 탄 아래로 이동한 뒤 뒤따르는 낮은 탄을 점프'],
],[118,90,AW-208])

chapter('8. GD05 - 다섯 내부 공격의 원본 코드')
para('이 장은 실제 다섯 공격 함수를 전부 싣는다. 이동 탄의 생성 위치·수명 계산은 다음 장의 공통 보조 함수를 함께 보면 된다. 각 함수의 Wait(Warning(...))는 전조이고, state=Attack 이후가 실제 공격 구간이다.')
code('trial','IEnumerator TrialHorizontal(','낮은 파동 2개')
code('trial','IEnumerator TrialRain(','천장에서 떨어지는 봉인 5개')
para('낙하 조준의 aim은 전조 시작에 고정된다. x마다 반원 지붕 높이 sqrt(R²-dx²)를 구하고, 탄의 반너비까지 포함한 바깥쪽 모서리를 기준으로 지붕 아래에서 생성한다. 전조 중 계속 쫓아오는 낙하가 아니므로 예고를 보고 옆으로 이동할 수 있다.')
code('trial','IEnumerator TrialPincer(','양쪽 협공 4개')
code('trial','IEnumerator TrialPillars(','바닥 기둥 5개')
para('기둥 중심 간격은 6.8, 너비는 2.34라 시각적 빈 폭은 4.46이다. 플레이어 너비 1.4를 제외해도 중심을 놓을 공간이 있다. 기둥은 0.3초 피해 판정 후 사라지며 GD04처럼 물리 발판을 남기는 패턴은 아니다.')
code('trial','IEnumerator TrialWeave(','높은 탄 뒤에 낮은 탄을 이어 발사')
para('높은 탄 중심은 y=5.4, 낮은 탄은 y=1.8이다. 먼저 높은 탄 2개, 0.85초 뒤 반대쪽에서 낮은 탄 2개를 낸다. 이 0.85초는 Warning을 통하지 않는 고정 대기다.')
code('trial','        Vector2 TrialWaveSize','탄 크기·반원 경계·이동 수명','before:        SpriteRenderer[] TrialWaveMarks')
code('trial','SpriteRenderer[] TrialWaveMarks(','두 발의 경로 전조')
code('trial','void LaunchTrialWaves(','경로 전조를 실제 탄으로 전환')
para('낮은 파동은 기본 WaveSize에 1.3을 곱한다. 현재 기본 크기 2.4×1.0이므로 3.12×1.3이다. 같은 쪽 두 탄의 시작 간격은 5.2, 속도는 20이다. 앞에 있는 탄은 이미 이동할 거리가 짧으므로 수명에서도 그 간격을 뺀다.')

chapter('9. GD05 - 성공 보상과 룸 자체의 폭발')
story.append(Sequence(['시험 완료\n피격 수 판정','성공: GD01 해제\n결계 0.4초 소멸','4초 공격 기회\n정상 피해 적용','재생 대기\n기본 8초 + 예고']))
para('<b>성공:</b> SurvivalTrial의 성공 분기에서 BreakAutoShield를 호출한다. 결계를 0.4초에 걸쳐 해제한 뒤 완전 무적도 풀고 4초 Recover에 들어간다. 플레이어의 기본 공격 60이 GD01에 의해 18로 줄어들지 않고 60 그대로 들어간다. 이미 GD01이 없었어도 재생 대기는 성공 시점부터 새로 예약한다.')
para('<b>실패:</b> 룸을 그대로 둔 채 0.95초 과부하 전조를 보이고 dome.Explode를 호출한다. 0.18초간 반원 안의 플레이어에게 폭발 접촉을 검사하고 0.57초 뒤 룸·이동 제한을 정리한다. 실패는 GD01을 해제하지 않는다. 별도 거대 투사체가 아니라 결계 껍질과 균열이 터지는 연출이다.')
code('dome','public bool Overlaps(','반원 폭발과 플레이어 사각형의 겹침')
explain('플레이어 사각형에서 반원 중심에 가장 가까운 점을 구한다. 그 점의 거리 제곱이 반지름 제곱 이하면 폭발과 겹친다. 플레이어 중심 한 점만 검사하지 않아 몸 일부가 겹친 경우도 반영한다.',
    '폭발은 하나의 접촉 기록을 공유해 0.18초 동안 여러 프레임 검사해도 한 번만 처리한다. 기본 폭발 피해는 14×1.15×2.5=40.25다. 타이밍을 맞춘 일반 반격은 가능하지만 GD02 내려베기 전용 플래그가 없어 폭발 반격으로 GD01을 깨지는 않는다.',
    '성공 보상 중에는 Recovery가 방어막 재생을 막는다. 8초 대기와 0.8초 예고는 일반 재생 규칙을 재사용한다. 실패 회복에서도 기존 GD01은 유지된다.')
call('<b>가장 최근 변경:</b> “GD01은 패링으로만 해제”는 현재 최종형의 전체 규칙이 아니다. 이제 GD02 내려베기 패링 또는 GD05 시험 성공으로 해제한다. 이 문장을 기준으로 팀의 이전 문서·기획 표현을 맞추면 된다.')
para('두 최종 계승 순서의 일반·강화 상태에서 성공/실패, 성공 4초, 정상 피해 60, 실패 후 감소 피해 18, 보호막 표시, 재생 지연·예고를 실제 게임에서 확인했다. 이 변경의 전용 검증은 28개다.')

chapter('10. 공통 판정·기절·중단 정리')
heading('한 번 맞는 공격을 여러 프레임 검사할 수 있는 구조')
code('rules','public sealed class KarmaAttackContact','접촉 기록: 피해 여부와 처리 소진의 분리')
para('Consumed는 접촉 시도를 이미 처리했다는 뜻이고 DealtDamage는 그 시도에서 실제 피해가 있었다는 뜻이다. 둘을 분리해야 패링·무적으로 막힌 접촉을 피해나 기절로 오인하지 않는다. GD05의 여러 탄은 한 묶음에 기록 하나를 공유해 연속 중복 피격을 막는다. 따라서 한 번 패링한 묶음의 다른 탄은 화면에 남아 있어도 추가 판정이 소진되어 있을 수 있다.')
code('rules','public static bool SweptBox(','두 프레임 사이의 이동 경로 검사')
code('rules','static bool Axis(','축별 진입·이탈 구간 교차')
para('공격의 이전 위치와 다음 위치를 선분으로 보고, 플레이어 반크기까지 확장한 사각형에 그 선분이 들어가는지 검사한다. first와 last는 선분이 사각형 안에 있는 시간 비율 구간이다. X와 Y의 구간이 동시에 겹치면 타격이다. 빨라진 파편도 다음 프레임 위치 한 점만 검사하지 않아 플레이어를 건너뛰는 현상을 줄인다.')
code('player','public void Stun(','플레이어 피격 기절과 연속 피격 보호')
para('피격 기절 중에는 이동·점프 입력을 비우고 속도를 0으로 만든다. 기절 종료 후 0.25초까지 피격 무적을 보장한다. 따라서 여러 추적 파편이 연속으로 도착해도 기절이 매번 끝없이 연장되는 구조는 아니다.')
code('present','void ClearPatternObjects()','효과 루트의 즉시 비활성화와 삭제')
code('state','void InterruptPattern()','일반 패턴 중단')
code('state','public void CancelCombat()','사망·전투 종료 정리')
explain('일반 중단은 진행 중인 코루틴·공격·전조·파편·계단을 멈추고 지운다. 결계 점수와 이동 제한, GD02 입력 예약도 함께 비운다. GD01은 일반 중단으로 지우지 않는다.',
    '전투 종료 CancelCombat은 일반 중단에 더해 GD01과 보호막 표시까지 정리한다. 같은 정리를 여러 번 호출하지 않도록 stopped를 확인한다.',
    'GD02 패링은 중단 후 BreakAutoShield, GD05 성공은 성공 분기 안의 BreakAutoShield로 해제한다. 공통 Recovery에서 GD01을 무조건 지우면 다른 패턴을 회피하는 것만으로 방어막이 사라져 규칙이 깨진다.')

chapter('11. 회의에서 짚을 핵심과 검증 범위')
table(['항목','설명할 내용'],[
    ['패링 입력과 성공','K를 큐 안에서 저장하는 것과 실제 내려베기 접촉 시 패링이 성립하는 것은 다른 단계다.'],
    ['GD01과 완전 무적','autoShield는 30% 피해를 받는 상태, fullShield는 피해를 받지 않는 상태다.'],
    ['GD02 충격파 조건','검기 계승과 HP 50% 이하를 모두 만족해야 한다. 충격파 반격은 방어막 해제용 패링이 아니다.'],
    ['GD03 시간과 방향','추적 시간은 거리로 계산하지만 추적 목표는 매 프레임 갱신한다. 복귀는 정지 때 저장한 두 점만 사용한다.'],
    ['GD04 벽과 후방','배치를 줄여도 플레이어 방향을 유지한다. 지면 방향은 고정하고 보스만 뒤로 돌아 GD02를 쓴다.'],
    ['GD05 점수','실제 피해 횟수를 세며 허용 1회면 0~1회 성공이다. 중간 실패 확정이어도 시험을 끝낸 뒤 폭발한다.'],
    ['GD05 성공 보상','현재는 성공 시 GD01도 해제한다. 성공 보상은 강화 속도 배율과 관계없이 4초다.'],
    ['동시 실행의 완료','파편마다 추적 길이가 달라 모든 파편의 완료를 기다려야 한다. 중단은 코루틴과 오브젝트를 함께 정리한다.'],
],[115,AW-115])
heading('최근 검증 기록 - 서로 다른 변경 시점의 실행이다')
table(['검증','기록 수','확인 범위'],[
    ['GD02/GD04 수정','222개','계승형·좌우·중앙/벽 사거리, 배치, 실제 이동과 K 패링'],
    ['GD03 원거리·가속','79개','정지/이동 대상 도달, 속도, 복귀, 개별 완료와 정리'],
    ['GD03 기존 연결','40개','투척→분열→순차 추적, 외형, 복귀 피해만 적용'],
    ['GD05 성공 보상','28개','두 최종 계승 순서, 성공/실패, 정상 피해, 4초 보상과 재생'],
    ['기존 GD01~GD05 전체','245개','GD03 가속·GD05 성공 해제 변경 이전의 전체 회귀 실행'],
],[147,60,AW-207])
para('최근 전용 검증과 이전 전체 검증을 한 번에 수행한 전체 테스트처럼 합산하지 않았다. 이 문서는 현재 소스를 설명하며, 사람의 장시간 플레이 난이도 평가나 모든 입력 조합의 무결성을 보장하는 문서는 아니다. 실행 기록은 저장소 output/validation에 남아 있다.','small')
heading('핵심 변수 사전')
table(['이름','뜻'],[
    ['selected / previous','현재 실행할 공격 / 직전 선택한 공격'],
    ['running','한 패턴의 코루틴이 진행 중인지 표시'],
    ['FightSeconds','일시정지 중 증가하지 않는 보스 전투 시간'],
    ['autoShield / fullShield','GD01 지속 방어막 / GD05 등의 완전 무적'],
    ['heavyParryQueued / pendingParry','큐 안에서 저장된 K 입력 / 실제 패링 성공 후 중단 예약'],
    ['direction / terrainDirection','현재 보스 진행 방향 / GD04 시전 때 고정한 지면 방향'],
    ['from / stopped / home','파편의 조준 출발점 / 추적 정지점 / 복귀할 보스 위치'],
    ['Consumed / DealtDamage','접촉 시도 소진 / 그 접촉에서 실제 피해 발생'],
],[190,AW-190])

chapter('부록. 원본 코드 위치와 추출 검수')
para('핵심 패턴 함수는 원본에서 연속 구간으로 추출했으며, HeavyStrike·SplitShield·ShieldFragment·RisingBarrier·RiseBarrierWave·RiseBarrierStep·SurvivalTrial과 다섯 결계 공격 함수는 본문에 함수 전체를 실었다. 보조 코드는 필요한 함수 또는 원본의 명시된 분기를 싣는다. 임의의 의사코드나 중간 줄 생략 기호로 대체하지 않았다.')
para('왼쪽 숫자는 실제 원본 줄 번호다. 지면 폭 때문에 긴 줄이 접히면 다음 표시 줄에는 번호를 반복하지 않는다. 공통 들여쓰기만 줄여 표시했고, 원문 표현·순서는 유지했다. 이 PDF는 아래 해시의 작업 트리 스냅샷을 기준으로 하므로 이후 코드가 바뀌면 줄 번호가 달라질 수 있다.')
for path, data in sources.items():
    para(f'<b>{escape(Path(path).name)}</b><br/>{escape(path)}<br/>SHA-256 앞 16자리: {data["sha256"][:16]}','small')
heading('원본 블록 찾아가기')
rows=[]
for r in excerpts:
    rows.append([r['id'],Path(r['path']).name,f"L{r['start']}-L{r['end']}"])
table(['블록','파일','원본 줄'],rows,[52,AW-140,88])

def header(c,doc):
    c.saveState(); c.setStrokeColor(colors.HexColor('#D9E3ED')); c.line(M,H-31,W-M,H-31)
    c.setFont('KRB',8.1); c.setFillColor(BLUE); c.drawString(M,H-23,'K4RMA  /  방어형 보스 패턴 · 원본 코드 해설')
    c.setFont('KR',7.7); c.setFillColor(MUTED); c.drawString(M,22,'2026-10-31  |  feat/minjae-boss-guard')
    c.drawRightString(W-M,22,f'{doc.page:02d}')
    c.restoreState()

doc=Doc(str(OUT),pagesize=A4,rightMargin=M,leftMargin=M,topMargin=49,bottomMargin=40,
    title='K4RMA 방어형 보스 패턴 - 현재 동작과 원본 코드 해설',author='K4RMA 개발 회의 자료',pageCompression=1)
doc.build(story,onFirstPage=header,onLaterPages=header)

# Verify every selected line against the current workspace after PDF creation.
for rec in excerpts:
    current=(ROOT/rec['path']).read_text(encoding='utf-8-sig').splitlines()
    assert current[rec['start']-1:rec['end']] == rec['raw_lines'], rec['id']
manifest={'created':STAMP,'branch':BRANCH,'pdf':str(OUT),'sources':{k:{'sha256':v['sha256'],'lines':len(v['lines'])} for k,v in sources.items()},'excerpts':excerpts}
(TMP/'source-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
from pypdf import PdfReader
reader=PdfReader(OUT)
text='\n'.join(p.extract_text() or '' for p in reader.pages)
(TMP/'extracted-text.txt').write_text(text,encoding='utf-8')
assert len(reader.pages)>12
for needle in ['GD01','GD02','GD03','GD04','GD05','BreakAutoShield','fragmentChaseSpeed','chaseSeconds','scaleDuration','0.4']:
    assert needle in text,needle
assert '\ufffd' not in text
print(json.dumps({'pdf':str(OUT),'pages':len(reader.pages),'source_blocks':len(excerpts),'source_files':len(sources),'source_lines':sum(len(x['raw_lines']) for x in excerpts),'bytes':OUT.stat().st_size},ensure_ascii=False))
