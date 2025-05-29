import aiohttp
import logging
import time
import base64
import hashlib
import json as json_module  # Đổi tên để tránh xung đột với tham số
from datetime import datetime
from typing import Dict, Any, Optional, Tuple
from Crypto.Cipher import AES
from Crypto.Util.Padding import pad
import os

# Thiết lập logging
logging.basicConfig(
    level=logging.DEBUG,  # Thay đổi thành DEBUG để thấy thông tin chi tiết hơn
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("debug_api.log", encoding='utf-8'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger("api_client")

class ApiResponse:
    """Đối tượng phản hồi từ API"""
    
    def __init__(self, data: Any = None, error: Any = None):
        self.data = data
        self.error = error
    
    def is_success(self) -> bool:
        """Kiểm tra phản hồi có thành công không"""
        # Kiểm tra có data không
        if self.data is None:
            logger.debug(f"API Response data is None, error: {self.error}")
            return False
            
        # Kiểm tra error field
        if "error" in self.data and self.data["error"] is not None:
            logger.debug(f"API Response has error: {self.data['error']}")
            return False
        
        # Nếu response chứa data và data chứa result, coi là thành công
        if "data" in self.data and "result" in self.data["data"]:
            # Chỉ kiểm tra responseCode nếu nó tồn tại và không phải null
            if ("responseCode" in self.data["data"] and 
                self.data["data"]["responseCode"] is not None and 
                self.data["data"]["responseCode"] != "000"):
                logger.debug(f"API Data response code not success: {self.data['data']['responseCode']}")
                return False
            logger.debug(f"API Response with data.result is considered successful")
            return True
            
        # Nếu response có result trực tiếp, cũng coi là thành công
        if "result" in self.data:
            # Kiểm tra responseCode trong result nếu tồn tại và không phải null
            if ("responseCode" in self.data["result"] and 
                self.data["result"]["responseCode"] is not None and 
                self.data["result"]["responseCode"] != "000"):
                logger.debug(f"API Result response code not success: {self.data['result']['responseCode']}")
                return False
            logger.debug(f"API Response with result is considered successful")
            return True
        
        # Kiểm tra responseCode ở cấp cao nhất, nếu tồn tại và không phải null
        if ("responseCode" in self.data and 
            self.data["responseCode"] is not None and 
            self.data["responseCode"] != "000"):
            logger.debug(f"API Response code not success: {self.data['responseCode']}")
            return False
        
        # Trong trường hợp không có các pattern đã biết, mặc định coi là thành công
        # nếu không có lỗi rõ ràng và có dữ liệu
        logger.debug(f"API Response is considered successful by default")
        return True
    
    @classmethod
    def from_dict(cls, response_dict: Dict) -> 'ApiResponse':
        """Tạo đối tượng ApiResponse từ dictionary"""
        # In thông tin chi tiết về response để debug
        logger.debug(f"Creating ApiResponse from: {json_module.dumps(response_dict, indent=2)}")
        return cls(
            data=response_dict.get('data'),
            error=response_dict.get('error')
        )

def encrypted_vm_uid(phone: str) -> str:
    """
    Mã hóa số điện thoại thành vmUID 
    
    Args:
        phone: Số điện thoại cần mã hóa
        
    Returns:
        str: Chuỗi đã được mã hóa
    """
    # Tạo khóa bí mật từ timestamp ngày hiện tại
    today = datetime.now()
    start_of_day = datetime.combine(today.date(), datetime.min.time())
    unix_timestamp = int(start_of_day.timestamp())
    hex_timestamp = ''.join([format(ord(c), 'x') for c in str(unix_timestamp)])
    
    # Padding khóa đến 32 ký tự
    secret_key = hex_timestamp.zfill(32)[:32]
    
    # Padding dữ liệu đến 32 ký tự
    data = phone.ljust(32, '|')[:32]
    
    # Chuyển đổi thành hex
    hex_data = ''.join([format(ord(c), '02x') for c in data])
    
    # Mã hóa AES
    iv = bytes([0] * 16)  # IV với 16 bytes 0
    cipher = AES.new(secret_key.encode('utf-8'), AES.MODE_CBC, iv)
    encrypted_data = cipher.encrypt(pad(hex_data.encode('utf-8'), AES.block_size))
    
    # Mã hóa base64
    return base64.b64encode(encrypted_data).decode('utf-8')

class ApiClient:
    """Client để tương tác với API"""
    
    BASE_URL = 'https://api-dev.vietmap.live/production'
    
    def __init__(self, session_limit: int = 50, timeout: int = 30):
        """
        Khởi tạo API client
        
        Args:
            session_limit: Giới hạn số phiên đồng thời
            timeout: Thời gian timeout cho mỗi request (giây)
        """
        self.timeout = timeout
        self.session = None
    
    async def create_session(self):
        """Tạo session HTTP mới nếu chưa tồn tại"""
        if self.session is None or self.session.closed:
            self.session = aiohttp.ClientSession(
                timeout=aiohttp.ClientTimeout(total=self.timeout)
            )
        return self.session
    
    async def close_session(self):
        """Đóng session HTTP nếu đang mở"""
        if self.session and not self.session.closed:
            await self.session.close()
    
    async def _make_api_call(self, method: str, url: str, headers: Dict = None, json_data: Dict = None, retry: int = 3) -> ApiResponse:
        """
        Thực hiện cuộc gọi API với retry
        
        Args:
            method: Phương thức HTTP (get, post, put, delete)
            url: URL endpoint
            headers: Headers của request
            json_data: Body của request (đổi tên để tránh xung đột)
            retry: Số lần thử lại
            
        Returns:
            ApiResponse: Kết quả từ API
        """
        headers = headers or {}
        session = await self.create_session()
        attempt = 0
        
        logger.debug(f"API Call {method.upper()} {url}")
        logger.debug(f"Headers: {headers}")
        logger.debug(f"Body: {json_data}")
        
        while attempt < retry:
            try:
                async with getattr(session, method)(url, headers=headers, json=json_data) as response:
                    response_text = await response.text()
                    logger.debug(f"API Response status: {response.status}")
                    logger.debug(f"API Response text: {response_text}")
                    
                    try:
                        # Sử dụng json_module thay vì json để tránh xung đột
                        response_data = json_module.loads(response_text)
                        logger.debug(f"API Response parsed: {json_module.dumps(response_data, indent=2)}")
                        return ApiResponse.from_dict(response_data)
                    except Exception as parse_error:
                        logger.error(f"Failed to parse JSON response: {response_text}, error: {str(parse_error)}")
                        return ApiResponse(error=f"Invalid JSON response: {response_text}")
                        
            except Exception as e:
                attempt += 1
                logger.error(f"API call attempt {attempt}/{retry} failed: {str(e)}")
                if attempt >= retry:
                    return ApiResponse(error=f"API call failed after {retry} attempts: {str(e)}")
                await asyncio.sleep(1)  # Chờ 1 giây trước khi thử lại
    
    async def login_by_password(self, phone: str, password: str = "123456", device_info: str = "") -> ApiResponse:
        """
        Đăng nhập bằng mật khẩu
        
        Args:
            phone: Số điện thoại
            password: Mật khẩu
            device_info: Thông tin thiết bị
            
        Returns:
            ApiResponse: Kết quả từ API
        """
        url = f"{self.BASE_URL}/v2/user/LoginByPassword"
        
        # Tạo vmUID header
        vm_uid = encrypted_vm_uid(phone)
        
        headers = {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'vmUID': vm_uid
        }
        
        payload = {
            "password": password,
            "phone": phone,
            "deviceInfo": device_info
        }
        
        logger.debug(f"Gọi LoginByPassword cho số điện thoại {phone}")
        return await self._make_api_call("post", url, headers=headers, json_data=payload)
    
    async def start_session(self, access_token: str, device_info: str = "") -> ApiResponse:
        """
        Bắt đầu phiên làm việc
        
        Args:
            access_token: Token xác thực
            device_info: Thông tin thiết bị
            
        Returns:
            ApiResponse: Kết quả từ API
        """
        url = f"{self.BASE_URL}/v16/user/StartSession"
        
        headers = {
            'accept': 'application/json',
            'Authorization': f'Bearer {access_token}',
            'Content-Type': 'application/json'
        }
        
        payload = {
            "deviceInfo": "S10A | QCM6125 | suding1384e982bb53312088479"
        }
        
        logger.debug(f"Gọi StartSession với token: {access_token[:10]}...")
        return await self._make_api_call("post", url, headers=headers, json_data=payload)

class UserSession:
    """Lưu trữ thông tin phiên của người dùng"""
    
    def __init__(self, phone: str, user_id: Optional[int] = None):
        """
        Khởi tạo phiên người dùng
        
        Args:
            phone: Số điện thoại
            user_id: ID người dùng (nếu có)
        """
        self.phone = phone
        self.user_id = user_id
        self.login_response = None
        self.session_response = None
        self.access_token = None  # từ login API
        self.refresh_token = None  # từ login API
        self.session_token = None  # là accessToken từ startSession API
        self.error = None
        self.last_activity = time.time()
        
    async def initialize(self, api_client: ApiClient, use_cache: bool = True) -> bool:
        """
        Khởi tạo phiên: đăng nhập và lấy thông tin kết nối MQTT
        
        Args:
            api_client: Đối tượng ApiClient để thực hiện cuộc gọi API
            use_cache: Sử dụng cache nếu có
            
        Returns:
            bool: True nếu khởi tạo thành công, False nếu thất bại
        """
        # Thử đọc từ cache trước nếu được yêu cầu
        if use_cache:
            cache_success = self.load_from_cache()
            if cache_success:
                logger.info(f"Loaded session from cache for {self.phone}")
                return True
                
        try:
            # 1. Đăng nhập
            login_result = await api_client.login_by_password(phone=self.phone)
            logger.debug(f"Login result for {self.phone}: {json_module.dumps(login_result.data, indent=2) if login_result.data else 'null'}")
            
            if not login_result.is_success():
                self.error = f"Đăng nhập thất bại: {login_result.error}"
                logger.error(f"Login failed for {self.phone}: {login_result.error}")
                return False
            
            self.login_response = login_result.data
            
            # Lưu token từ API login - thay đổi cách truy cập để phù hợp với cấu trúc API thực tế
            if 'data' in login_result.data and 'result' in login_result.data['data']:
                # Trường hợp 1: result nằm trong data
                result = login_result.data['data']['result']
                self.access_token = result.get('accessToken')
                self.refresh_token = result.get('refreshToken')
                logger.debug(f"Extracted tokens from data.result for {self.phone}")
                
            elif 'result' in login_result.data:
                # Trường hợp 2: result nằm trong root
                result = login_result.data['result']
                self.access_token = result.get('accessToken')
                self.refresh_token = result.get('refreshToken')
                logger.debug(f"Extracted tokens from result for {self.phone}")
                
            else:
                # Trường hợp 3: thử tìm trực tiếp
                self.access_token = login_result.data.get('accessToken')
                self.refresh_token = login_result.data.get('refreshToken')
                logger.debug(f"Extracted tokens from root for {self.phone}")
            
            if not self.access_token or not self.refresh_token:
                self.error = "Không tìm thấy accessToken hoặc refreshToken trong phản hồi đăng nhập"
                logger.error(f"Missing tokens for {self.phone}: access_token={self.access_token}, refresh_token={self.refresh_token}")
                return False
            
            logger.info(f"Login successful for {self.phone}: access_token={self.access_token[:10]}..., refresh_token={self.refresh_token[:10]}...")
            
            # 2. Bắt đầu phiên
            session_result = await api_client.start_session(access_token=self.access_token)
            logger.debug(f"StartSession result for {self.phone}: {json_module.dumps(session_result.data, indent=2) if session_result.data else 'null'}")
            
            if not session_result.is_success():
                self.error = f"Bắt đầu phiên thất bại: {session_result.error}"
                logger.error(f"Session start failed for {self.phone}: {session_result.error}")
                return False
            
            self.session_response = session_result.data
            
            # Trích xuất session token (accessToken từ API startSession)
            # Kiểm tra nhiều cấu trúc có thể có
            session_info = None
            
            if 'data' in session_result.data and 'result' in session_result.data['data']:
                session_info = session_result.data['data']['result']
                logger.debug(f"Using session info from data.result for {self.phone}")
                
            elif 'result' in session_result.data:
                session_info = session_result.data['result']
                logger.debug(f"Using session info from result for {self.phone}")
                
            else:
                session_info = session_result.data
                logger.debug(f"Using session info from root for {self.phone}")
            
            # Lưu session token - đây chính là password cho kết nối MQTT
            self.session_token = session_info.get('accessToken')
            
            if not self.session_token:
                # Thử tìm trong các vị trí khác
                if isinstance(session_info, dict) and 'data' in session_info:
                    self.session_token = session_info['data'].get('accessToken')
                
            if not self.session_token:
                self.error = "Không tìm thấy accessToken trong phản hồi StartSession"
                logger.error(f"Missing session token for {self.phone}")
                return False
            
            logger.info(f"Session started successfully for {self.phone}: session_token={self.session_token[:10]}...")
            self.last_activity = time.time()
            
            # Lưu vào cache nếu thành công
            self.save_to_cache()
            
            return True
            
        except Exception as e:
            self.error = f"Lỗi khởi tạo phiên: {str(e)}"
            logger.error(f"Exception during initialization for {self.phone}: {str(e)}", exc_info=True)
            return False
    
    def get_mqtt_connection_info(self) -> Dict[str, Any]:
        """
        Lấy thông tin kết nối MQTT
        
        Returns:
            Dict: Thông tin kết nối MQTT
        """
        if not self.session_response:
            logger.error(f"No session response for {self.phone}")
            return {}
        
        # Lấy thông tin MQTT từ session response - kiểm tra nhiều cấu trúc có thể có
        session_info = None
        
        if 'data' in self.session_response and 'result' in self.session_response['data']:
            session_info = self.session_response['data']['result']
        elif 'result' in self.session_response:
            session_info = self.session_response['result']
        else:
            session_info = self.session_response
        
        # Theo yêu cầu:
        # - clientId, username = refreshToken từ API login
        # - password = accessToken từ API startSession
        # - Pub/Sub topics từ API startSession
        
        # Log chi tiết thông tin kết nối MQTT
        connection_info = {
            "clientId": self.refresh_token,
            "username": self.refresh_token,
            "password": self.session_token,  # accessToken từ startSession
            "host": session_info.get("host", "localhost"),
            "port": session_info.get("port", 1883),
            "pubTopics": session_info.get("pubTopics", []),
            "subTopics": session_info.get("subTopics", []),
            "phone": self.phone,
            "user_id": self.user_id
        }
        
        logger.debug(f"MQTT Connection info for {self.phone}: {json_module.dumps(connection_info, indent=2)}")
        return connection_info
        
    def to_dict(self) -> Dict[str, Any]:
        """
        Chuyển đổi thông tin phiên thành dictionary để lưu cache
        
        Returns:
            Dict: Thông tin phiên dưới dạng dictionary
        """
        return {
            "phone": self.phone,
            "user_id": self.user_id,
            "login_response": self.login_response,
            "session_response": self.session_response,
            "access_token": self.access_token,
            "refresh_token": self.refresh_token,
            "session_token": self.session_token,
            "last_activity": self.last_activity
        }
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'UserSession':
        """
        Tạo đối tượng UserSession từ dictionary đọc từ cache
        
        Args:
            data: Dictionary chứa thông tin phiên
            
        Returns:
            UserSession: Đối tượng đã được khởi tạo
        """
        session = cls(
            phone=data["phone"],
            user_id=data["user_id"]
        )
        session.login_response = data["login_response"]
        session.session_response = data["session_response"]
        session.access_token = data["access_token"]
        session.refresh_token = data["refresh_token"]
        session.session_token = data["session_token"]
        session.last_activity = data["last_activity"]
        return session
    
    def save_to_cache(self) -> bool:
        """
        Lưu thông tin phiên vào cache
        
        Returns:
            bool: True nếu lưu thành công, False nếu thất bại
        """
        try:
            # Tạo thư mục cache nếu chưa tồn tại
            cache_dir = "session_cache"
            os.makedirs(cache_dir, exist_ok=True)
            
            # Tạo tên file cache
            cache_file = os.path.join(cache_dir, f"session_{self.phone}.json")
            
            # Lưu vào file
            with open(cache_file, 'w', encoding='utf-8') as f:
                json.dump(self.to_dict(), f, indent=2)
                
            logger.info(f"Saved session cache for {self.phone}")
            return True
        except Exception as e:
            logger.error(f"Error saving session cache for {self.phone}: {str(e)}")
            return False
    
    def load_from_cache(self) -> bool:
        """
        Đọc thông tin phiên từ cache
        
        Returns:
            bool: True nếu đọc thành công, False nếu thất bại hoặc cache không tồn tại
        """
        try:
            # Tạo tên file cache
            cache_dir = "session_cache"
            cache_file = os.path.join(cache_dir, f"session_{self.phone}.json")
            
            # Kiểm tra file tồn tại
            if not os.path.exists(cache_file):
                logger.debug(f"No session cache found for {self.phone}")
                return False
                
            # Đọc từ file
            with open(cache_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            # Kiểm tra dữ liệu hợp lệ
            required_fields = ["access_token", "refresh_token", "session_token", "session_response"]
            for field in required_fields:
                if field not in data or not data[field]:
                    logger.warning(f"Invalid session cache for {self.phone}: missing {field}")
                    return False
            
            # Cập nhật thông tin
            self.login_response = data["login_response"]
            self.session_response = data["session_response"]
            self.access_token = data["access_token"]
            self.refresh_token = data["refresh_token"]
            self.session_token = data["session_token"]
            self.last_activity = data["last_activity"]
            
            logger.info(f"Loaded session cache for {self.phone}")
            return True
        except Exception as e:
            logger.error(f"Error loading session cache for {self.phone}: {str(e)}")
            return False

# Import asyncio để sử dụng trong hàm _make_api_call
import asyncio
import json