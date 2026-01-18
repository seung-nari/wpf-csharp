Dataset은 “영상 전체 컨테이너”다.
안에 뭐가 들어있냐면:

Dataset
 ├─ RasterXSize  (가로 픽셀 수)
 ├─ RasterYSize  (세로 픽셀 수)
 ├─ RasterCount  (밴드 개수)
 ├─ GeoTransform (좌표 변환 정보)
 ├─ Projection   (좌표계)
 └─ Band 1
     Band 2
     Band 3

👉 픽셀 값은 Dataset 안에 직접 없다
👉 항상 Band를 통해서만 접근한다

Dataset ds
 : “이 변수는 영상 전체를 대표하는 객체다”

Gdal.Open(...)
 -“파일 경로를 GDAL에게 넘긴다”
 - 성공하면 Dataset 생성
 - 실패하면 null

Access.GA_ReadOnly
 - 읽기 전용
 - 실무에서 기본값

dataset.GetRasterBand()
 - 이 영상에서 1번 밴드에 해당하는 ‘2차원 픽셀 데이터 층’을 가져온다
👉 Band = 이미지 자체가 아니라, 이미지 안의 ‘데이터 레이어’

Band 안에 들어있는 것들
| 항목       | 설명                               |
| --------   | ----------------------------       |
| 픽셀 값    | 각 (x, y)에 대응되는 숫자 값       |
| DataType   | Byte / UInt16 / Float32 등         |
| NoData 값  | 유효하지 않은 픽셀 값              |
| 통계 정보  | Min / Max / Mean (있을 수도 없음)  |
| 색상 해석  | Gray, Palette, RGB 등              |
| 크기       | width × height (Dataset과 동일)   |

👉 Band = “픽셀 값의 2차원 배열 + 메타 정보”

Band1 DataType : GDT_Byte
Bands : 1

이 파일은:
 - 단일 밴드(1개) + 8비트 흑백 영상


픽셀 하나의 의미

0   → 검정
255 → 흰색
중간 → 회색

그래서 이건 그레이스케일 위성영상 / DEM 음영 / 흑백 항공사진 같은 타입

Band 안의 픽셀은 이런 구조:
(row 0)  [ 123 ][ 124 ][ 122 ] ...
(row 1)  [ 120 ][ 119 ][ 121 ]
(row 2)  [ 118 ][ 117 ][ 119 ]

col = x 방향
row = y 방향

y(row)
↓
0   [123] [124] [122]
1   [120] [119] [121]
2   [118] [117] [119]
     0     1     2  → x(col)

✅ 실제 영상 처리 세계
 - 배열은 [row][col]
 - 좌표는 (x, y)
 - 접근은 (col, row)

 _band.ReadRaster(col, row, 1, 1, buffer, 1, 1, 0, 0);
 => col, row 위치에 있는 픽셀 하나를 읽어라

col = 1;
row = 2;

👉 읽는 값은 (1,2) = 117

그럼 RGB 이미지는 어떻게 되냐?
| Band 번호 | 의미    |
| -------   | -----   |
| Band 1    | Red     |
| Band 2    | Green   |
| Band 3    | Blue    |

Band r = ds.GetRasterBand(1);
Band g = ds.GetRasterBand(2);
Band b = ds.GetRasterBand(3);